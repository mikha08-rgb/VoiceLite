-- Make userId column optional to fix license creation
-- This fixes the "Null constraint violation on the fields: (userId)" error

-- Check if userId column exists and make it nullable
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'License'
        AND column_name = 'userId'
    ) THEN
        ALTER TABLE "License" ALTER COLUMN "userId" DROP NOT NULL;
    END IF;
END $$;
