-- DropForeignKey
ALTER TABLE "UserActivity" DROP CONSTRAINT "UserActivity_userId_fkey";

-- DropTable
DROP TABLE "UserActivity";

-- DropEnum
DROP TYPE "ActivityType";

