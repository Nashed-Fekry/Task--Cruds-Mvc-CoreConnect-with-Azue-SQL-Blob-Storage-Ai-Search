using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UploadImage.Data;
using UploadImage.Models;
using UploadImage.ViewModel;

namespace UploadImage.Controllers
{
    public class StudentsController : Controller
    {
        private readonly UploadImageContext _context;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly SearchClient _searchClient;
        private readonly IConfiguration _configuration;
        private readonly string containerName = "personimg";

        public StudentsController(UploadImageContext context, BlobServiceClient blobServiceClient, IConfiguration configuration, SearchClient searchClient)
        {
            _context = context;
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            _searchClient = searchClient;
        }






        public async Task<IActionResult> Index(string searchString, string selectedCourse, int? pageNumber)
        {
            int pageSize = 5;

            // Build the query
            IQueryable<Student> query = _context.Student;

            // Apply search filter
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(s => s.Name.Contains(searchString) || s.Address.Contains(searchString));
            }

            // Apply course filter
            if (!string.IsNullOrEmpty(selectedCourse))
            {
                query = query.Where(s => s.Course == selectedCourse);
            }

            // Paginate the results
            var paginatedStudents = await PaginatedList<Student>.CreateAsync(query.OrderBy(s => s.Course), pageNumber ?? 1, pageSize);

            ViewData["SearchString"] = searchString;
            ViewData["SelectedCourse"] = selectedCourse;
            return View(paginatedStudents);
        }


        //public async Task<IActionResult> Index(string searchString, string selectedCourse, int? pageNumber)
        //{
        //    int pageSize = 5;

        //    // Build the search options
        //    SearchOptions options = new SearchOptions
        //    {
        //        IncludeTotalCount = true,
        //        Skip = (pageNumber ?? 1 - 1) * pageSize,
        //        Size = pageSize,
        //        OrderBy = { $"Course asc" } // Sort by course in ascending order
        //    };

        //    // Build the search filters
        //    List<string> filters = new List<string>();
        //    if (!string.IsNullOrEmpty(searchString))
        //    {
        //        filters.Add($"Name eq '{searchString}' or Address eq '{searchString}'");
        //    }
        //    if (!string.IsNullOrEmpty(selectedCourse))
        //    {
        //        filters.Add($"Course eq '{selectedCourse}'");
        //    }

        //    string searchText = string.Join(" and ", filters);

        //    // Perform the search
        //    SearchResults<Student> searchResults = await _searchClient.SearchAsync<Student>(searchText, options);

        //    // Extract the results and apply additional filters
        //    var studentIds = searchResults.GetResults().Select(r => r.Document.Id).ToList();

        //    // Retrieve students from the database using the retrieved student IDs
        //    var students = await _context.Student.Where(s => studentIds.Contains(s.Id)).ToListAsync();

        //    // Paginate the results
        //    var paginatedStudents = new PaginatedList<Student>(
        //        students,
        //        (int)searchResults.TotalCount,
        //        pageNumber ?? 1,
        //        pageSize);

        //    ViewData["SearchString"] = searchString;
        //    ViewData["SelectedCourse"] = selectedCourse;
        //    return View(paginatedStudents);
        //}



        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Student.FirstOrDefaultAsync(m => m.Id == id);
            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(StudentViewModel vm)
        {
            string stringFileName = UploadFileToBlobStorage(vm);
            var student = new Student
            {
                Id = Guid.NewGuid(),
                Name = vm.Name,
                Image = stringFileName,
                Age = vm.Age,
                Address = vm.Address,
                PhoneNumber = vm.PhoneNumber,
                Course = vm.Course
            };

            _context.Student.Add(student);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Student.FindAsync(id);
            if (student == null)
            {
                return NotFound();
            }
            return View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Image,Age,Address,PhoneNumber,Course")] Student student, StudentViewModel vm)
        {
            if (id != student.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingStudent = await _context.Student.FindAsync(id);
                    if (existingStudent == null)
                    {
                        return NotFound();
                    }

                    _context.Entry(existingStudent).State = EntityState.Detached;

                    // Check if a new image is uploaded
                    if (vm.Image != null)
                    {
                        // Delete the existing image
                        if (!string.IsNullOrEmpty(existingStudent.Image))
                        {
                            DeleteImageFromBlobStorage(existingStudent.Image);
                        }

                        // Upload the new image
                        string stringFileName = UploadFileToBlobStorage(vm);
                        student.Image = stringFileName;
                    }
                    else
                    {
                        // If no new image is uploaded, retain the existing image
                        student.Image = existingStudent.Image;
                    }

                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }

        private bool StudentExists(Guid id)
        {
            return _context.Student.Any(e => e.Id == id);
        }

        private string UploadFileToBlobStorage(StudentViewModel vm)
        {
            if (vm.Image == null)
            {
                return null;
            }

            string connectionString = _configuration.GetConnectionString("BlobStorageConnection");

            string fileName = Guid.NewGuid().ToString() + "-" + vm.Image.FileName;

            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            using (Stream stream = vm.Image.OpenReadStream())
            {
                blobClient.UploadAsync(stream, true);
            }

            return fileName;
        }

        public async Task<IActionResult> GetImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return NotFound();
            }
            string connectionString = _configuration.GetConnectionString("BlobStorageConnection");
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            var response = await blobClient.DownloadAsync();
            var contentType = response.Value.ContentType;

            return File(response.Value.Content, contentType);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {

            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Student.FirstOrDefaultAsync(m => m.Id == id);
            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {

            var student = await _context.Student.FindAsync(id);
            if (student == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(student.Image))
            {
                await DeleteImageFromBlobStorage(student.Image);
            }

            _context.Student.Remove(student);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task DeleteImageFromBlobStorage(string fileName)
        {
            string connectionString = _configuration.GetConnectionString("BlobStorageConnection");
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            await blobClient.DeleteIfExistsAsync();
        }
    }
}
