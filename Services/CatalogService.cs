using TelegramBot.Models;

namespace TelegramBot.Services
{
    public static class CatalogService
    {
        public const string AdditionalCategoryName = "Дополнительно";
        public const string ManicureCategoryName = "Маникюр";
        public const string PedicureCategoryName = "Педикюр";

        public static readonly IReadOnlyList<string> StaticCategoryNames = new[]
        {
            ManicureCategoryName,
            PedicureCategoryName,
            AdditionalCategoryName
        };

        public static async Task EnsureStaticCategoriesAsync()
        {
            var res = await SupabaseConfig.GetClient().From<ServiceCategory>().Get();
            var existing = res.Models ?? new List<ServiceCategory>();

            foreach (var name in StaticCategoryNames)
            {
                if (existing.Any(c => string.Equals(c.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var cat = new ServiceCategory { CategoryId = Guid.NewGuid(), Name = name };
                var inserted = await SupabaseConfig.GetClient().From<ServiceCategory>().Insert(cat);
                var created = inserted.Models?.FirstOrDefault();
                if (created != null)
                    existing.Add(created);
            }
        }

        public static async Task<List<ServiceCategory>> GetCategoriesAsync()
        {
            var res = await SupabaseConfig.GetClient().From<ServiceCategory>().Get();
            var all = res.Models ?? new List<ServiceCategory>();
            return all
                .Where(c => StaticCategoryNames.Any(n =>
                    string.Equals(c.Name?.Trim(), n, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => StaticCategoryNames.ToList().FindIndex(n =>
                    string.Equals(n, c.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public static async Task<List<Service>> GetAllServicesAsync()
        {
            var response = await SupabaseConfig.GetClient().From<Service>().Get();
            return response.Models ?? new List<Service>();
        }

        public static async Task<List<Service>> GetByCategoryAsync(Guid categoryId, bool additionalOnly = false)
        {
            var all = await GetAllServicesAsync();
            var cats = await GetCategoriesAsync();
            var addCat = cats.FirstOrDefault(c => c.Name == AdditionalCategoryName);

            if (additionalOnly && addCat != null)
                return all.Where(s => s.CategoryId == addCat.CategoryId).ToList();

            if (!additionalOnly && addCat != null)
                return all.Where(s => s.CategoryId == categoryId && s.CategoryId != addCat.CategoryId).ToList();

            return all.Where(s => s.CategoryId == categoryId).ToList();
        }

        public static async Task<List<Service>> GetMainServicesAsync()
        {
            var cats = await GetCategoriesAsync();
            var addId = cats.FirstOrDefault(c => c.Name == AdditionalCategoryName)?.CategoryId;
            var all = await GetAllServicesAsync();
            return all.Where(s => s.CategoryId != addId).ToList();
        }

        public static async Task<List<Service>> GetAdditionalServicesAsync()
        {
            var cats = await GetCategoriesAsync();
            var addCat = cats.FirstOrDefault(c => c.Name == AdditionalCategoryName);
            if (addCat == null) return new List<Service>();
            return await GetByCategoryAsync(addCat.CategoryId);
        }

        public static async Task<List<Service>> GetRequestServicesAsync(Guid requestId)
        {
            var items = await SupabaseConfig.GetClient().From<RequestItem>().Where(ri => ri.RequestId == requestId).Get();
            if (!items.Models.Any()) return new List<Service>();

            var serviceIds = items.Models.Select(i => i.ServiceId).ToList();
            var all = await GetAllServicesAsync();
            return all.Where(s => serviceIds.Contains(s.ServiceId)).ToList();
        }

        public static async Task<string> FormatServiceListAsync(IEnumerable<Guid> serviceIds)
        {
            var all = await GetAllServicesAsync();
            var names = all.Where(s => serviceIds.Contains(s.ServiceId)).Select(s => s.Name);
            return string.Join(", ", names);
        }

        public static async Task<Service?> AddServiceAsync(Guid categoryId, string name, string description, int duration, decimal price)
        {
            var svc = new Service
            {
                ServiceId = Guid.NewGuid(),
                CategoryId = categoryId,
                Name = name,
                Description = description,
                DurationMinutes = duration,
                Price = price
            };
            var res = await SupabaseConfig.GetClient().From<Service>().Insert(svc);
            return res.Models?.FirstOrDefault();
        }

        public static async Task<bool> DeleteServiceAsync(Guid serviceId)
        {
            try
            {
                await SupabaseConfig.GetClient().From<RequestItem>().Where(ri => ri.ServiceId == serviceId).Delete();
                await SupabaseConfig.GetClient().From<Service>().Where(s => s.ServiceId == serviceId).Delete();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteServiceAsync: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> HasAnyServicesAsync()
        {
            var all = await GetAllServicesAsync();
            return all.Count > 0;
        }
    }
}
