-- Отзывы: рейтинг 1–5, один отзыв на запись.
-- Supabase → SQL Editor → Run → Settings → API → Reload schema (обязательно!)

CREATE TABLE IF NOT EXISTS reviews (
    review_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    client_id uuid NOT NULL,
    appointment_id uuid,
    text text NOT NULL DEFAULT '',
    rating integer
);

ALTER TABLE reviews ADD COLUMN IF NOT EXISTS review_id uuid;
ALTER TABLE reviews ADD COLUMN IF NOT EXISTS client_id uuid;
ALTER TABLE reviews ADD COLUMN IF NOT EXISTS appointment_id uuid;
ALTER TABLE reviews ADD COLUMN IF NOT EXISTS text text;
ALTER TABLE reviews ADD COLUMN IF NOT EXISTS rating integer;

UPDATE reviews SET rating = 5 WHERE rating IS NULL;

ALTER TABLE reviews DROP CONSTRAINT IF EXISTS reviews_rating_check;
ALTER TABLE reviews ADD CONSTRAINT reviews_rating_check
    CHECK (rating IS NULL OR (rating >= 1 AND rating <= 5));

DROP INDEX IF EXISTS reviews_appointment_id_unique;
CREATE UNIQUE INDEX IF NOT EXISTS reviews_appointment_id_unique
    ON reviews (appointment_id)
    WHERE appointment_id IS NOT NULL;

GRANT SELECT, INSERT, UPDATE, DELETE ON reviews TO anon, authenticated;
GRANT ALL ON reviews TO service_role;
ALTER TABLE reviews DISABLE ROW LEVEL SECURITY;

-- После выполнения: Dashboard → Project Settings → API → Reload schema
