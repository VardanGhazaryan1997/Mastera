using Microsoft.EntityFrameworkCore;

namespace Mastera.Infrastructure.Persistence;

/// <summary>
/// Task 5.2: PostgreSQL exclusion constraints that make double-booking impossible, even when two requests arrive
/// at the same moment. Two active bookings (Pending/Confirmed) of the same master can't overlap; neither can two holds.
/// Cancelled, completed and no-show bookings don't count. Applied after migrations (idempotent), like row-level security.
/// </summary>
public static class BookingConstraints
{
    public const string BookingsConstraint = "EX_Bookings_NoOverlap";
    public const string HoldsConstraint = "EX_SlotHolds_NoOverlap";

    public const string Sql = """
        CREATE EXTENSION IF NOT EXISTS btree_gist;
        DO $$
        BEGIN
            IF to_regclass('"Bookings"') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'EX_Bookings_NoOverlap') THEN
                ALTER TABLE "Bookings" ADD CONSTRAINT "EX_Bookings_NoOverlap"
                    EXCLUDE USING gist ("MasterId" WITH =, tstzrange("Start", "End") WITH &&)
                    WHERE ("Status" IN ('Pending', 'Confirmed'));
            END IF;
            IF to_regclass('"SlotHolds"') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'EX_SlotHolds_NoOverlap') THEN
                ALTER TABLE "SlotHolds" ADD CONSTRAINT "EX_SlotHolds_NoOverlap"
                    EXCLUDE USING gist ("MasterId" WITH =, tstzrange("Start", "End") WITH &&);
            END IF;
        END
        $$;
        """;

    public static void Ensure(DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Database.ExecuteSqlRaw(Sql);
    }

    public static async Task EnsureAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await context.Database.ExecuteSqlRawAsync(Sql, cancellationToken);
    }
}
