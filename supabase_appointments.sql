-- Если «Одобрить» заявку падает — выполните в SQL Editor

CREATE TABLE IF NOT EXISTS appointments (
    appointment_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    request_id uuid REFERENCES requests(request_id) ON DELETE SET NULL,
    client_id uuid NOT NULL REFERENCES clients(client_id) ON DELETE CASCADE,
    master_id uuid NOT NULL REFERENCES masters(master_id) ON DELETE CASCADE,
    working_date_id uuid NOT NULL REFERENCES working_dates(date_id) ON DELETE CASCADE,
    time_slot_id uuid NOT NULL REFERENCES time_slots(time_slot_id) ON DELETE CASCADE,
    status text DEFAULT 'confirmed'
);

ALTER TABLE appointments ADD COLUMN IF NOT EXISTS appointment_id uuid;
ALTER TABLE appointments ADD COLUMN IF NOT EXISTS request_id uuid;
ALTER TABLE appointments ADD COLUMN IF NOT EXISTS client_id uuid;
ALTER TABLE appointments ADD COLUMN IF NOT EXISTS master_id uuid;
ALTER TABLE appointments ADD COLUMN IF NOT EXISTS working_date_id uuid;
ALTER TABLE appointments ADD COLUMN IF NOT EXISTS time_slot_id uuid;
ALTER TABLE appointments ADD COLUMN IF NOT EXISTS status text DEFAULT 'confirmed';

DO $$
DECLARE r record;
BEGIN
  FOR r IN
    SELECT conname FROM pg_constraint
    WHERE conrelid = 'public.appointments'::regclass AND contype = 'c'
  LOOP
    EXECUTE format('ALTER TABLE appointments DROP CONSTRAINT IF EXISTS %I', r.conname);
  END LOOP;
END $$;

GRANT SELECT, INSERT, UPDATE, DELETE ON appointments TO anon, authenticated;
GRANT ALL ON appointments TO service_role;
ALTER TABLE appointments DISABLE ROW LEVEL SECURITY;
