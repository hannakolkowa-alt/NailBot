using TelegramBot.Services;

namespace TelegramBot.Data
{
    public static class DefaultCatalogSeed
    {
        public static async Task EnsureSeedAsync()
        {
            await CatalogService.EnsureStaticCategoriesAsync();

            if (await CatalogService.HasAnyServicesAsync())
                return;

            var cats = await CatalogService.GetCategoriesAsync();
            var manicure = cats.FirstOrDefault(c => c.Name == CatalogService.ManicureCategoryName);
            var pedicure = cats.FirstOrDefault(c => c.Name == CatalogService.PedicureCategoryName);
            var additional = cats.FirstOrDefault(c => c.Name == CatalogService.AdditionalCategoryName);

            if (manicure == null || pedicure == null || additional == null) return;

            await CatalogService.AddServiceAsync(manicure.CategoryId,
                "Гигиенический маникюр без покрытия",
                "Обработка кутикулы и ногтя (женский/мужской)", 60, 800);

            await CatalogService.AddServiceAsync(manicure.CategoryId,
                "Укрепление гелем",
                "База + гель + гель-лак (по желанию) + топ", 90, 1500);

            await CatalogService.AddServiceAsync(manicure.CategoryId,
                "Наращивание ногтей",
                "База + акригель + гель + гель-лак. Цена зависит от длины", 120, 2500);

            await CatalogService.AddServiceAsync(manicure.CategoryId,
                "Ремонт ногтя",
                "Реконструкция при сколах, трещинах или поломках", 30, 300);

            await CatalogService.AddServiceAsync(manicure.CategoryId,
                "Снятие материала",
                "Снятие, маникюр и лечебное покрытие", 60, 500);

            await CatalogService.AddServiceAsync(pedicure.CategoryId,
                "Без покрытия",
                "Обработка кутикулы и ногтей", 60, 1200);

            await CatalogService.AddServiceAsync(pedicure.CategoryId,
                "Гель-лак + педикюр",
                "Покрытие: база + гель-лак + топ", 90, 2000);

            await CatalogService.AddServiceAsync(additional.CategoryId, "Французский маникюр", "Дополнительная услуга", 15, 200);
            await CatalogService.AddServiceAsync(additional.CategoryId, "Дизайн", "Рисунки, стразы, стэмпинг, фигурки", 30, 100);
            await CatalogService.AddServiceAsync(additional.CategoryId, "Ремонт ногтя (до 3-х)", "Количество ногтей для ремонта до 3", 20, 300);
            await CatalogService.AddServiceAsync(additional.CategoryId, "Ремонт ногтя (от 3-х)", "Количество ногтей для ремонта от 3", 40, 500);

            Console.WriteLine("Log: Каталог услуг заполнен начальными данными");
        }
    }
}
