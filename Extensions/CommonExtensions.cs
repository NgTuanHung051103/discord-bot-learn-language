using Discord.WebSocket;
using System.Globalization;

namespace NTH.Extensions
{
    public static class CommonExtensions
    {
        public static DateTime? GetDayDefault(SocketSlashCommandDataOption? targetDate, bool isToday = true)
        {
            if (targetDate == null)
            {
                return isToday ? DateTime.Today : DateTime.Today.AddDays(1);
            }
            DateTime parsedDate;

            string? date = targetDate.Value.ToString();

            if (!string.IsNullOrWhiteSpace(date))
            {
                // Cố gắng parse giá trị từ option
                if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                {
                    return parsedDate;
                }
                else
                {
                    return null;
                }
            }
            return DateTime.Today.AddDays(1);
        }
    }
}
