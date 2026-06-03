-- Статические категории услуг (мастер не может добавлять новые категории).
-- Выполните в Supabase SQL Editor, затем Settings → API → Reload schema.

CREATE TABLE IF NOT EXISTS service_categories (
    category_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL UNIQUE
);

INSERT INTO service_categories (name)
VALUES ('Маникюр'), ('Педикюр'), ('Дополнительно')
ON CONFLICT (name) DO NOTHING;

-- Удалите лишние категории вручную, если они были созданы ранее:
-- DELETE FROM service_categories WHERE name NOT IN ('Маникюр', 'Педикюр', 'Дополнительно');
