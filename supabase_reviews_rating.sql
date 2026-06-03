-- Рейтинг 1–5 и один отзыв на запись.
-- Выполните в Supabase SQL Editor, затем Reload schema.

ALTER TABLE reviews ADD COLUMN IF NOT EXISTS rating integer;

UPDATE reviews SET rating = 5 WHERE rating IS NULL;

ALTER TABLE reviews DROP CONSTRAINT IF EXISTS reviews_rating_check;
ALTER TABLE reviews ADD CONSTRAINT reviews_rating_check CHECK (rating IS NULL OR (rating >= 1 AND rating <= 5));

CREATE UNIQUE INDEX IF NOT EXISTS reviews_appointment_id_unique
    ON reviews (appointment_id)
    WHERE appointment_id IS NOT NULL;

GRANT SELECT, INSERT, UPDATE, DELETE ON reviews TO anon, authenticated;
GRANT ALL ON reviews TO service_role;
