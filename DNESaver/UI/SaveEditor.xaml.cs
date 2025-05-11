using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DNESaver.UI
{
    public class EditableFact
    {
        public string DispFactName
        {
            get => String.IsNullOrWhiteSpace(FactDefinition.FactName) ? FactDefinition.FactId : FactDefinition.FactName ?? "";
        }

        public string DispLine2
        {
            get => $"Asset: {FactDefinition.FactAssetName}, Family: {FactDefinition.Family}";
        }

        public string ToolTip
        {
            get => $"{FactDefinition.FactAssetId}/{FactDefinition.FactId}";
        }

        public required FactDefinition FactDefinition { get; set; }

        public ValueWithDisplayname? NewValueBYTE { get; set; } = null;
        public int NewValueInt { get; set; } = 0;
        public float NewValueFloat { get; set; } = 0;

        public bool IsImportant { get; set; } = false;

        public string SortableValue { get => IsImportant ? $"____{index:D5}___{FactDefinition.FactName}{FactDefinition.FactId}" : $"{index:D5}___{FactDefinition.FactName}{FactDefinition.FactId}"; }

        public List<ValueWithDisplayname> EnumValues { get; set; } = [];
        public bool ChangeValue { get; set; } = false;

        public required FactTimeline Timeline { get; set; }
        public string TimelineString
        {
            get => Timeline.GetHistoryString(false);
        }

        public int index = 0;
    }

    public class EditableScene
    {
        public required string SceneId { get; set; }
        public required string SceneName { get; set; }
        public required Relationship[] Relationships { get; set; }
    }

    public class ValueWithDisplayname
    {
        public required byte Value { get; set; }
        public required string Display { get; set; }

        public override string ToString() => Display;
    }

    /// <summary>
    /// Interaction logic for SaveEditor.xaml
    /// </summary>
    public partial class SaveEditor : Window
    {
        private readonly static List<string> ImportantFacts = [
            "S4500_NoraLeaves",
            "Sxxxx_BAR_Ending_IsPresent_SUM",
            "Sxxxx_BAR_Ending_IsPresent_AUT",
            "S1000_DIA_CatColour",
            "S2000_DIA_EncouragedPamAndGus_Lvl1",
            "S2000_DIA_ChattedWithPam",
            "S4000_DIA_EncouragedPamAndGus_Lvl3",
            "S2780_DIA_EncouragedPamAndGus_Lvl2",
            "S2780_DIA_SpokeToGusPam",
        ];

        public SaveFilePatcher Patcher { get; }
        public SavegameModifier Modifier { get; }

        public SaveEditor(SaveFilePatcher patcher, SavegameModifier modifier)
        {
            InitializeComponent();
            EditableFactsView.SortDescriptions.Add(new SortDescription() { PropertyName = nameof(EditableFact.SortableValue), Direction = ListSortDirection.Ascending });
            Patcher = patcher;
            Modifier = modifier;

            int idx = 0;
            foreach (var fact in Modifier.CatalogFacts())
            {
                EditableFact ef = new()
                {
                    FactDefinition = fact.Fact,
                    Timeline = fact,
                    IsImportant = ImportantFacts.Contains(fact.Fact.FactName)
                };

                if (ef.FactDefinition.Type == FactType.BoolFact)
                {
                    ef.EnumValues =
                    [
                        new(){ Display = "false", Value = 0x00},
                        new(){ Display = "true", Value = 0x01},
                    ];
                }
                else if (ef.FactDefinition.Type == FactType.EnumFact)
                {
                    ef.EnumValues = ef.FactDefinition.EnumValues.Select(s => new ValueWithDisplayname() { Display = s.Name, Value = s.Numerical }).ToList();

                    if (ef.EnumValues.Count > 1 && ef.EnumValues.Last().Display.StartsWith("Value ")) ef.EnumValues = ef.EnumValues[..^1];
                }

                ef.index = idx++;

                EditableFacts.Add(ef);
            }

            foreach (var scene in modifier.GetRelationships())
            {
                EditableScenes.Add(new() { Relationships = scene.Value, SceneId = scene.Key, SceneName = scene.Key == "global" ? "Current Snapshot" : SavegameModifier.SceneNameMappings[scene.Key] });
            }

            foreach(var ec in EditableScenes.Skip(5))
            {
                if(ec.SceneName.StartsWith("1-"))
                {
                    ec.Relationships[0].level = 2;
                    ec.Relationships[1].level = 2;
                    ec.Relationships[2].level = 2;
                    ec.Relationships[3].level = 2;
                    ec.Relationships[4].level = 2;
                } else
                {
                    ec.Relationships[0].level = 5;
                    ec.Relationships[1].level = 5;
                    ec.Relationships[2].level = 2;
                    ec.Relationships[3].level = 5;
                    ec.Relationships[4].level = 2;
                }
            }
        }

        public ObservableCollection<EditableFact> EditableFacts { get; set; } = [];
        public ObservableCollection<EditableScene> EditableScenes { get; set; } = [];
        public ICollectionView EditableFactsView { get => CollectionViewSource.GetDefaultView(EditableFacts); }

        private void search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string st = search.Text;
            if (string.IsNullOrWhiteSpace(st)) EditableFactsView.Filter = a => true;
            else EditableFactsView.Filter = a => a is EditableFact ef && (
                ef.DispFactName.Contains(st, StringComparison.InvariantCultureIgnoreCase) ||
                ef.DispLine2.Contains(st, StringComparison.InvariantCultureIgnoreCase)
            );
        }

        private void SaveAndClose(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var fact in EditableFacts)
                {
                    if (!fact.ChangeValue) continue;
                    switch (fact.FactDefinition.Type)
                    {
                        case FactType.BoolFact:
                            if (fact.NewValueBYTE == null) continue;
                            Modifier.AdjustFact(fact.FactDefinition, new(fact.NewValueBYTE.Value > 0), SavegameModifier.SceneNameMappings.Keys, true);
                            break;
                        case FactType.EnumFact:
                            if (fact.NewValueBYTE == null) continue;
                            Modifier.AdjustFact(fact.FactDefinition, new(fact.NewValueBYTE.Value), SavegameModifier.SceneNameMappings.Keys, true);
                            break;
                        case FactType.IntFact:
                            Modifier.AdjustFact(fact.FactDefinition, new(fact.NewValueInt), SavegameModifier.SceneNameMappings.Keys, true);
                            break;
                        case FactType.FloatFact:
                            Modifier.AdjustFact(fact.FactDefinition, new(fact.NewValueFloat), SavegameModifier.SceneNameMappings.Keys, true);
                            break;
                    }
                }

                foreach (var scene in EditableScenes)
                {
                    foreach (var rel in scene.Relationships)
                    {
                        if (scene.SceneId == "global")
                        {
                            Modifier.AdjustRelationship(rel, [], true);
                        }
                        else
                        {
                            Modifier.AdjustRelationship(rel, [scene.SceneId], false);
                        }
                    }
                }

                Patcher.WriteOut();
                WasSuccesful = true;

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Encountered an error while patching save file: \n" + ex.Message);
                WasSuccesful = false;
            }
        }

        public bool WasSuccesful = false;
    }
}
