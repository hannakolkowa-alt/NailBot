-- ОБЯЗАТЕЛЬНО: выполните целиком, если «requests_status_check» при подтверждении заявки
-- Удаляет ВСЕ check-ограничения на таблице requests (старые схемы курсовых проектов)

DO $$
DECLARE r record;
BEGIN
  FOR r IN
    SELECT conname
    FROM pg_constraint
    WHERE conrelid = 'public.requests'::regclass AND contype = 'c'
  LOOP
    EXECUTE format('ALTER TABLE requests DROP CONSTRAINT IF EXISTS %I', r.conname);
  END LOOP;
END $$;

ALTER TABLE requests ALTER COLUMN status SET DEFAULT 'pending';
UPDATE requests SET status = lower(trim(status)) WHERE status IS NOT NULL;

-- Не добавляем жёсткий CHECK — бот использует pending/approved/rejected/cancelled
