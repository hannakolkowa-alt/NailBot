-- Если заявка падает с requests_status_check — выполните в SQL Editor

ALTER TABLE requests DROP CONSTRAINT IF EXISTS requests_status_check;

ALTER TABLE requests ADD CONSTRAINT requests_status_check
    CHECK (status IN ('pending', 'approved', 'rejected', 'cancelled'));

UPDATE requests SET status = lower(status) WHERE status IS NOT NULL;
