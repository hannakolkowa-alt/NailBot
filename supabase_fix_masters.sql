-- ВАЖНО: выполните ПОСЛЕ supabase_schema.sql, если /master пишет «Ошибка таблицы masters»
-- CREATE TABLE IF NOT EXISTS не меняет уже существующую таблицу — этот скрипт дополняет колонки.

-- 1) Добавить недостающие колонки (безопасно, можно запускать несколько раз)
ALTER TABLE masters ADD COLUMN IF NOT EXISTS master_id uuid;
ALTER TABLE masters ADD COLUMN IF NOT EXISTS name text;
ALTER TABLE masters ADD COLUMN IF NOT EXISTS telegram_username text;
ALTER TABLE masters ADD COLUMN IF NOT EXISTS experience text;
ALTER TABLE masters ADD COLUMN IF NOT EXISTS description text;
ALTER TABLE masters ADD COLUMN IF NOT EXISTS created_at timestamptz DEFAULT now();
ALTER TABLE masters ADD COLUMN IF NOT EXISTS updated_at timestamptz DEFAULT now();

-- 2) Если раньше был столбец id вместо master_id (старые проекты)
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'masters' AND column_name = 'id'
  ) AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'masters' AND column_name = 'master_id'
  ) THEN
    UPDATE masters SET master_id = id WHERE master_id IS NULL;
  END IF;
END $$;

-- 3) Значения по умолчанию
UPDATE masters SET name = 'Мастер' WHERE name IS NULL;
UPDATE masters SET telegram_username = '' WHERE telegram_username IS NULL;
UPDATE masters SET experience = '' WHERE experience IS NULL;
UPDATE masters SET description = '' WHERE description IS NULL;
UPDATE masters SET created_at = now() WHERE created_at IS NULL;
UPDATE masters SET updated_at = now() WHERE updated_at IS NULL;

-- 4) Первичный ключ (если ещё нет)
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'masters_pkey' AND conrelid = 'public.masters'::regclass
  ) THEN
    ALTER TABLE masters ADD PRIMARY KEY (master_id);
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'PK masters: %', SQLERRM;
END $$;

-- 5) Права и RLS
GRANT SELECT, INSERT, UPDATE, DELETE ON masters TO anon, authenticated;
GRANT ALL ON masters TO service_role;
ALTER TABLE masters DISABLE ROW LEVEL SECURITY;

-- 6) working_dates / time_slots — колонки на случай старой схемы
ALTER TABLE working_dates ADD COLUMN IF NOT EXISTS date_id uuid;
ALTER TABLE working_dates ADD COLUMN IF NOT EXISTS master_id uuid;
ALTER TABLE working_dates ADD COLUMN IF NOT EXISTS date date;

ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS time_slot_id uuid;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS working_date_id uuid;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS time time;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS is_booked boolean DEFAULT false;

GRANT SELECT, INSERT, UPDATE, DELETE ON working_dates, time_slots TO anon, authenticated;
GRANT ALL ON working_dates, time_slots TO service_role;
ALTER TABLE working_dates DISABLE ROW LEVEL SECURITY;
ALTER TABLE time_slots DISABLE ROW LEVEL SECURITY;
