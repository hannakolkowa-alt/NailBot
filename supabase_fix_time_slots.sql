-- Выполните в Supabase SQL Editor, если время слота не сохраняется (10:00 → ошибка)

-- Старое имя колонки «время» → time
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'time_slots' AND column_name = 'время'
  ) AND NOT EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = 'public' AND table_name = 'time_slots' AND column_name = 'time'
  ) THEN
    ALTER TABLE time_slots RENAME COLUMN "время" TO time;
  END IF;
END $$;

CREATE TABLE IF NOT EXISTS time_slots (
    time_slot_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    working_date_id uuid REFERENCES working_dates(date_id) ON DELETE CASCADE,
    time time NOT NULL,
    is_booked boolean DEFAULT false
);

ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS time_slot_id uuid;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS working_date_id uuid;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS time time;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS is_booked boolean DEFAULT false;

-- Если time пустой, но есть старые данные без колонки time — пропустите

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conname = 'time_slots_pkey' AND conrelid = 'public.time_slots'::regclass
  ) THEN
    ALTER TABLE time_slots ADD PRIMARY KEY (time_slot_id);
  END IF;
EXCEPTION WHEN OTHERS THEN
  RAISE NOTICE 'PK time_slots: %', SQLERRM;
END $$;

-- Удалить НЕВЕРНЫЙ уникальный индекс только на time (мешает 10:00 на разных датах)
ALTER TABLE time_slots DROP CONSTRAINT IF EXISTS time_slots_время_key;
ALTER TABLE time_slots DROP CONSTRAINT IF EXISTS time_slots_time_key;
ALTER TABLE time_slots DROP CONSTRAINT IF EXISTS "time_slots_время_key";

-- Правильно: одно время нельзя дважды на ОДНУ дату, но на разные даты — можно
DROP INDEX IF EXISTS time_slots_working_date_time_unique;
CREATE UNIQUE INDEX IF NOT EXISTS time_slots_working_date_time_unique
    ON time_slots (working_date_id, time);

GRANT SELECT, INSERT, UPDATE, DELETE ON time_slots TO anon, authenticated;
GRANT ALL ON time_slots TO service_role;
ALTER TABLE time_slots DISABLE ROW LEVEL SECURITY;
