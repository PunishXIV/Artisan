using Lumina.Data;
using Lumina;

namespace Artisan.QuestSync
{
    internal class QuestData : ExcelRow
    {
        public string? Id { get; set; }
        public Lumina.Text.SeString? Text { get; set; }


        public override void PopulateData(RowParser parser, GameData gameData, Language language)
        {
            base.PopulateData(parser, gameData, language);

            this.Id = parser.ReadColumn<string>(0)!;
            this.Text = parser.ReadColumn<Lumina.Text.SeString>(1)!;
        }
    }
}
