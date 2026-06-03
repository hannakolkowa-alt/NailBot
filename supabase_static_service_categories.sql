-- Статические категории услуг (мастер не может добавлять новые категории).
-- Выполните в Supabase SQL Editor, затем Settings → API → Reload schema.

CREATE TABLE IF NOT EXISTS service_categories (
    category_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL UNIQUE
);

INSERT INTO service_categories (name)
VALUES ('Маникюр'), ('Педикюр')
ON CONFLICT (name) DO NOTHING;

-- Удаление устаревшей категории «Дополнительно» (и её услуг):
-- DELETE FROM request_items WHERE service_id IN (
--   SELECT service_id FROM services WHERE category_id IN (
--     SELECT category_id FROM service_categories WHERE name = 'Дополнительно'));
-- DELETE FROM services WHERE category_id IN (
--   SELECT category_id FROM service_categories WHERE name = 'Дополнительно');
-- DELETE FROM service_categories WHERE name = 'Дополнительно';
