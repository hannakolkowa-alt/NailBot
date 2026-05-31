-- Выполните в Supabase SQL Editor, если сохранение мастера/расписания падает
-- Если таблица masters УЖЕ была — после этого обязательно supabase_fix_masters.sql

-- masters
CREATE TABLE IF NOT EXISTS masters (
    master_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name text NOT NULL DEFAULT 'Мастер',
    telegram_username text DEFAULT '',
    experience text DEFAULT '',
    description text DEFAULT '',
    created_at timestamptz DEFAULT now(),
    updated_at timestamptz DEFAULT now()
);

-- working_dates
CREATE TABLE IF NOT EXISTS working_dates (
    date_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    master_id uuid NOT NULL REFERENCES masters(master_id) ON DELETE CASCADE,
    date date NOT NULL,
    UNIQUE (master_id, date)
);

-- time_slots (если ещё нет)
CREATE TABLE IF NOT EXISTS time_slots (
    time_slot_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    working_date_id uuid REFERENCES working_dates(date_id) ON DELETE CASCADE,
    time time NOT NULL,
    is_booked boolean DEFAULT false
);

ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS working_date_id uuid REFERENCES working_dates(date_id);
ALTER TABLE time_slots ADD COLUMN IF NOT EXISTS is_booked boolean DEFAULT false;
