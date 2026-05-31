-- Сначала выполните supabase_permissions.sql (права на таблицы)
-- Затем этот файл, если колонок ещё нет в time_slots

ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS working_date_id uuid REFERENCES working_dates(date_id);
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS is_booked boolean DEFAULT false;
-- Если колонка называлась "время", переименуйте:
-- ALTER TABLE time_slots RENAME COLUMN "время" TO time;
