-- Выполните в Supabase SQL Editor, если «Ошибка при создании заявки»

CREATE TABLE IF NOT EXISTS clients (
    client_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    telegram_id bigint NOT NULL UNIQUE,
    first_name text DEFAULT 'Клиент',
    telegram_username text DEFAULT ''
);

CREATE TABLE IF NOT EXISTS requests (
    request_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    client_id uuid NOT NULL REFERENCES clients(client_id) ON DELETE CASCADE,
    desired_date date,
    desired_time time,
    comment text DEFAULT '',
    status text DEFAULT 'pending',
    created_at timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS request_items (
    request_id uuid NOT NULL REFERENCES requests(request_id) ON DELETE CASCADE,
    service_id uuid NOT NULL,
    quantity int DEFAULT 1,
    PRIMARY KEY (request_id, service_id)
);

-- Дополнить старые таблицы
ALTER TABLE clients ADD COLUMN IF NOT EXISTS client_id uuid;
ALTER TABLE clients ADD COLUMN IF NOT EXISTS telegram_id bigint;
ALTER TABLE clients ADD COLUMN IF NOT EXISTS first_name text;
ALTER TABLE clients ADD COLUMN IF NOT EXISTS telegram_username text;

ALTER TABLE requests ADD COLUMN IF NOT EXISTS request_id uuid;
ALTER TABLE requests ADD COLUMN IF NOT EXISTS client_id uuid;
ALTER TABLE requests ADD COLUMN IF NOT EXISTS desired_date date;
ALTER TABLE requests ADD COLUMN IF NOT EXISTS desired_time time;
ALTER TABLE requests ADD COLUMN IF NOT EXISTS comment text;
ALTER TABLE requests ADD COLUMN IF NOT EXISTS status text DEFAULT 'pending';
ALTER TABLE requests ADD COLUMN IF NOT EXISTS created_at timestamptz DEFAULT now();

ALTER TABLE request_items ADD COLUMN IF NOT EXISTS request_id uuid;
ALTER TABLE request_items ADD COLUMN IF NOT EXISTS service_id uuid;
ALTER TABLE request_items ADD COLUMN IF NOT EXISTS quantity int DEFAULT 1;

GRANT SELECT, INSERT, UPDATE, DELETE ON clients, requests, request_items TO anon, authenticated;
GRANT ALL ON clients, requests, request_items TO service_role;

ALTER TABLE clients DISABLE ROW LEVEL SECURITY;
ALTER TABLE requests DISABLE ROW LEVEL SECURITY;
ALTER TABLE request_items DISABLE ROW LEVEL SECURITY;
