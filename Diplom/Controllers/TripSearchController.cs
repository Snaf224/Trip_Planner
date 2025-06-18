using Microsoft.AspNetCore.Mvc;
using Diplom.Models;
using Diplom.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

public class TripSearchController : Controller
{
    private readonly ApplicationDbContext _context;

    public TripSearchController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: форма поиска
    public IActionResult Index()
    {
        var model = new TripSearch
        {
            TravelDate = DateTime.Today
        };

        var upcomingTrips = _context.Trips
            .Where(t => t.StartDate >= DateTime.Today)
            .OrderBy(t => t.StartDate)
            .Take(3)
            .ToList();

        ViewBag.UpcomingTrips = upcomingTrips;

        return View(model);
    }

    // POST: Поиск поездок
    [HttpPost]
    public IActionResult Search(TripSearch searchModel)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.UpcomingTrips = _context.Trips
                .Where(t => t.StartDate >= DateTime.Today)
                .OrderBy(t => t.StartDate)
                .Take(3)
                .ToList();

            return View("Index", searchModel);
        }

        var availableTrips = _context.Trips
            .Include(t => t.Members)
            .Where(t => t.Budget <= searchModel.Budget &&
                        t.StartDate <= searchModel.TravelDate &&
                        t.EndDate >= searchModel.TravelDate)
            .ToList();

        availableTrips = availableTrips
            .Where(t => t.Members.Count + 2 >= searchModel.NumberOfPeople)
            .ToList();

        ViewBag.AvailableTrips = availableTrips;
        ViewBag.SearchModel = searchModel;

        ViewBag.UpcomingTrips = _context.Trips
            .Where(t => t.StartDate >= DateTime.Today)
            .OrderBy(t => t.StartDate)
            .Take(3)
            .ToList();

        return View("Index", searchModel);
    }

    // POST: Бронирование поездки
    [HttpPost]
    public IActionResult Book(int tripId, string userName, string userEmail, int numberOfPeople)
    {
        var trip = _context.Trips
            .Include(t => t.Members)
            .FirstOrDefault(t => t.Id == tripId);

        if (trip == null)
        {
            TempData["Error"] = "Поездка не найдена!";
            return RedirectToAction("Index");
        }

        int existingMembersCount = trip.Members.Count;

        var newMembers = new List<TripMember>();

        // Добавляем бронирующего
        newMembers.Add(new TripMember
        {
            Name = userName,
            Contribution = 0,
            Status = "Участник, ожидает подтверждения",
            ContactInfo = userEmail,
            TripId = tripId
        });

        // Добавляем его компаньонов
        for (int i = 1; i < numberOfPeople; i++)
        {
            string participantEmail = $"participant{i}.{userName.Replace(" ", ".").ToLower()}@example.com";
            newMembers.Add(new TripMember
            {
                Name = $"Участник {i + 1} (с {userName})",
                Contribution = 0,
                Status = "Участник",
                ContactInfo = participantEmail,
                TripId = tripId
            });
        }

        // Считаем общее число участников после добавления новых
        int totalMembersCount = existingMembersCount + newMembers.Count;

        // Формируем сообщение для уведомления
        string message;

        if (existingMembersCount == 0)
        {
            message = $"Новый участник {userName} присоединился к поездке.";
        }
        else
        {
            string joinedToText = (totalMembersCount - 1) switch
            {
                1 => " с 1 человеком",
                _ => $" с {totalMembersCount - 1} людьми"
            };

            message = $"Новый участник {userName} присоединился к поездке{joinedToText}.";
        }

        // Создаем одно уведомление для всего трипа, чтобы не спамить
        _context.Notifications.Add(new Notification
        {
            Message = message,
            SendDate = DateTime.Now,
            IsRead = false,
            ReceiverId = null, // общее уведомление, без конкретного получателя
            TripId = tripId
        });

        _context.TripMembers.AddRange(newMembers);
        _context.SaveChanges();

        TempData["Success"] = "Вы успешно забронировали поездку!";
        return RedirectToAction("Index");
    }
}
