using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Diplom.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Diplom.Models;

namespace Diplom.Controllers
{
    public class UploadController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UploadController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public UploadController(
            IWebHostEnvironment environment,
            ILogger<UploadController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _environment = environment;
            _logger = logger;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> UploadPhoto()
        {
            var user = await _userManager.GetUserAsync(User);
            string? avatarPath = user?.AvatarPath; // Если AvatarPath = null, ничего не подгружаем

            ViewBag.AvatarPath = avatarPath;
            await SetUserInfoAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadPhoto(IFormFile photo)
        {
            var user = await _userManager.GetUserAsync(User);
            string? avatarPath = user?.AvatarPath; // По умолчанию используем текущий AvatarPath

            if (photo != null && photo.Length > 0)
            {
                try
                {
                    // Генерация хэша от содержимого фото для предотвращения дубликатов
                    var hash = GenerateFileHash(photo);
                    var fileExtension = Path.GetExtension(photo.FileName);
                    var fileName = $"{hash}{fileExtension}";
                    var savePath = Path.Combine(_environment.WebRootPath, "media", fileName);

                    // Если файл с таким хэшем уже существует, используем его
                    if (!System.IO.File.Exists(savePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                        await using var stream = new FileStream(savePath, FileMode.Create);
                        await photo.CopyToAsync(stream);
                        _logger.LogInformation("Avatar uploaded and saved at: {Path}", savePath);
                    }

                    avatarPath = $"/media/{fileName}";

                    // Сохраняем путь в базе данных
                    if (user != null)
                    {
                        user.AvatarPath = avatarPath;
                        var result = await _userManager.UpdateAsync(user);
                        if (!result.Succeeded)
                        {
                            _logger.LogError("Failed to update user avatar path in database.");
                        }
                    }

                    // Удаляем старые дубликаты
                    DeleteOldDuplicates(hash, fileExtension);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error uploading file: {Message}", ex.Message);
                }
            }

            ViewBag.AvatarPath = avatarPath;
            await SetUserInfoAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ClearPhoto()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user != null)
            {
                user.AvatarPath = null;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to clear user avatar path in database.");
                }
            }

            ViewBag.AvatarPath = null; // Ничего не подгружаем
            await SetUserInfoAsync();
            return View("UploadPhoto");
        }

        private async Task SetUserInfoAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                ViewBag.Email = user?.Email;
                ViewBag.PhoneNumber = string.IsNullOrEmpty(user?.PhoneNumber) ? "Не указан" : user.PhoneNumber;
                ViewBag.Gender = user?.GetLocalizedGender() ?? "Не указан";
            }
            else
            {
                ViewBag.Email = "Неизвестно";
                ViewBag.PhoneNumber = "не указан";
                ViewBag.Gender = "Неизвестно";
            }
        }

        private string GenerateFileHash(IFormFile file)
        {
            using var md5 = MD5.Create();
            using var stream = file.OpenReadStream();
            var hashBytes = md5.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void DeleteOldDuplicates(string hash, string fileExtension)
        {
            var directoryPath = Path.Combine(_environment.WebRootPath, "media");
            var files = Directory.GetFiles(directoryPath, $"{hash}*");

            // Оставляем только текущий файл, остальные удаляем
            foreach (var file in files)
            {
                if (Path.GetFileName(file) != $"{hash}{fileExtension}" && System.IO.File.Exists(file))
                {
                    System.IO.File.Delete(file);
                    _logger.LogInformation("Duplicate file deleted: {File}", file);
                }
            }
        }
    }
}