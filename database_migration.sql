-- Сначала: supabase_permissions.sql
-- Для слотов времени: supabase_fix_time_slots.sql

ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS working_date_id uuid REFERENCES working_dates(date_id);
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS is_booked boolean DEFAULT false;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS time_slot_id uuid;
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS time time;

-- Переименовать «время» → time (старые проекты)
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
