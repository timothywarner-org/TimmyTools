namespace TimmyTools.Core.Migrations;

public class Schema7To8Migration : SchemaMigration
{
    public override int TargetSchemaVersion => 7;
    public override int ResultingSchemaVersion => 8;
    public override string UpdateQuery => $@"
        ALTER TABLE Settings ADD COLUMN BreakTimer_Segment INTEGER DEFAULT 0;

        -- Update schema version
        UPDATE SchemaInfo
        SET Version = {ResultingSchemaVersion}
        WHERE Id = 0;
    ";
}
