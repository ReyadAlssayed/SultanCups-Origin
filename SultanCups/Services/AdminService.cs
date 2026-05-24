using SultanCups.Data;
using SultanCups.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace SultanCups.Services
{
    public class AdminService
    {
        private readonly AppDbContext _context;

        // عميل HTTP لإرسال الطلبات إلى Telegram API
        private readonly HttpClient _http = new HttpClient();

        // توكن البوت
        private const string BotToken = "8500953804:AAGll8E2_FhATfsgwRFlXuydhr_0M-uG2hA";

        // ChatId الخاص بالمطورِ}
        private const string ChatId = "6321706551";

        public AdminService(AppDbContext context)
        {
            _context = context;
        }

        // إرسال رسالة دعم فني من النظام إلى المطور عبر بوت Telegram
        public async Task SendSupportMessage(string userName, string role, string message)
        {
            var text =
        $@"📩 رسالة دعم فني

المستخدم: {userName}
الصلاحية: {role}

الرسالة:
{message}";

            var url = $"https://api.telegram.org/bot{BotToken}/sendMessage";

            using var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("chat_id", ChatId),
        new KeyValuePair<string, string>("text", text)
    });

            var response = await _http.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        // هذا الجزء خاص بجدول admins
        // جلب جميع المسؤولين من قاعدة البيانات
        public async Task<List<Admain>> GetAdmins()
        {
            return await _context.admins
                .AsNoTracking() // للعرض فقط، توفر الذاكرة والوقت
                .ToListAsync();
        }

        // هذا الجزء خاص بجدول admins
        // حذف مسؤول حسب admin_id
        public async Task<bool> DeleteAdmin(int adminId)
        {
            var admin = await _context.admins
                .FirstOrDefaultAsync(a => a.admin_id == adminId);

            if (admin == null)
                return false;

            admin.is_active = false;

            await _context.SaveChangesAsync();

            return true;
        }

        // هذا الجزء خاص بجدول admins
        // تعديل بيانات مسؤول موجود
        public async Task<bool> UpdateAdmin(Admain updatedAdmin)
        {
            var admin = await _context.admins.FirstOrDefaultAsync(a => a.admin_id == updatedAdmin.admin_id);

            if (admin == null)
                return false;

            admin.full_name = updatedAdmin.full_name;
            admin.username = updatedAdmin.username;
            admin.phone = updatedAdmin.phone;
            admin.role = updatedAdmin.role;
            admin.is_active = updatedAdmin.is_active;

            admin.password_hash = updatedAdmin.password_hash;

            await _context.SaveChangesAsync();
            return true;
        }

        // جلب حالة التخزين (حجم قاعدة البيانات + مساحة القرص المتبقية)
        public async Task<(double usedMb, double freeGb)> GetStorageStatus()
        {
            // استعلام لجلب حجم القاعدة بالبايت وتحويله لميجابايت
            var sql = "SELECT pg_database_size(current_database());";
            using var conn = new Npgsql.NpgsqlConnection(_context.Database.GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new Npgsql.NpgsqlCommand(sql, conn);

            var sizeBytes = Convert.ToDouble(await cmd.ExecuteScalarAsync());
            double usedDbMb = sizeBytes / 1024.0 / 1024.0;

            // مساحة الهارد ديسك الحقيقية
            var drive = new System.IO.DriveInfo("C");
            double freeGb = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;

            return (Math.Round(usedDbMb, 2), Math.Round(freeGb, 2));
        }

        public async Task<bool> UsernameExists(string username)
        {
            return await _context.admins
                .AnyAsync(x => x.username.ToLower() == username.ToLower());
        }

        public async Task<bool> PhoneExists(string phone)
        {
            return await _context.admins
                .AnyAsync(x => x.phone == phone);
        }

        public async Task<bool> CreateAdmin(Admain model)
        {
            _context.admins.Add(model);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Admain?> LoginAsync(string username, string password)
        {
            username = username.Trim();

            var admin = await _context.admins
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.username.ToLower() == username.ToLower()
                    && x.is_active);

            if (admin == null)
                return null;

            bool valid =
                BCrypt.Net.BCrypt.Verify(password, admin.password_hash);

            if (!valid)
                return null;

            return admin;
        }

        //ملاحظات عامة في النظام

        public async Task AddQuickNote(string text)
        {
            var note = new QuickNote
            {
                note_text = text,
                created_at = DateTime.UtcNow
            };

            _context.quick_notes.Add(note);

            await _context.SaveChangesAsync();
        }

        public async Task<List<QuickNote>> GetQuickNotes()
        {
            return await _context.quick_notes
                .AsNoTracking()
                .OrderByDescending(x => x.note_id)
                .ToListAsync();
        }

        public async Task DeleteQuickNote(int id)
        {
            var note = await _context.quick_notes
                .FirstOrDefaultAsync(x => x.note_id == id);

            if (note == null)
                return;

            _context.quick_notes.Remove(note);

            await _context.SaveChangesAsync();
        }

    }
}