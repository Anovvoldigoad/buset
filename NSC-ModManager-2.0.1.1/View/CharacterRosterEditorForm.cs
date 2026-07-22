using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NSC_ModManager.Model;
using NSC_ModManager.ViewModel;

namespace NSC_ModManager.View
{
    /// <summary>
    /// WinForms replacement for CharacterRosterEditorView.xaml/.xaml.cs. Same
    /// CharacterRosterEditorViewModel, same slot-reordering logic (ReplaceSlots,
    /// ConvertCostumeToSlot, MoveOnlyBaseCostume, SwapCostumeIndices) as the
    /// original -- only the drag-drop *mechanism* changed, from WPF's
    /// VisualTreeHelper hit-testing to WinForms' ListBox.IndexFromPoint, since
    /// that's the WinForms-native way to find "which item is under the mouse".
    /// </summary>
    public partial class CharacterRosterEditorForm : Form
    {
        private readonly CharacterRosterEditorViewModel VM;

        private ListBox characterListBox, placeholderListBox, costumeListBox, placeholderCostumeListBox;
        private NumericUpDown pageUpDown;

        private string dragSourceListName;
        private int dragSourceIndex = -1;

        public CharacterRosterEditorForm(TitleViewModel titleVM)
        {
            VM = new CharacterRosterEditorViewModel(titleVM);
            Text = Loc("m_rosterEditor");
            Width = 900;
            Height = 800;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
        }

        private void BuildUI()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            root.Controls.Add(new Label { Text = Loc("m_rosterEditorSlotGame"), AutoSize = true }, 0, 0);
            root.Controls.Add(new Label { Text = Loc("m_rosterEditorCostumePlaceHolder"), AutoSize = true }, 1, 0);
            root.Controls.Add(new Label { Text = Loc("m_rosterEditorSlotPlaceHolder"), AutoSize = true }, 2, 0);
            root.Controls.Add(new Label { Text = "Placeholder Costumes", AutoSize = true }, 3, 0);

            characterListBox = MakeIconListBox("CharacterIconListPreview", VM.CharacterList);
            costumeListBox = MakeIconListBox("CharacterCostumeIconListPreview", VM.CostumePlaceHolderList);
            placeholderListBox = MakeIconListBox("CharacterPlaceHolderIconListPreview", VM.CharacterPlaceHolderList);
            placeholderCostumeListBox = MakeIconListBox("PlaceholderCostumeIconListPreview", VM.PlaceholderCostumePlaceHolderList);

            characterListBox.SelectedIndexChanged += (s, e) => VM.SelectedCharacterIndex = characterListBox.SelectedIndex;

            root.Controls.Add(characterListBox, 0, 1);
            root.Controls.Add(costumeListBox, 1, 1);
            root.Controls.Add(placeholderListBox, 2, 1);
            root.Controls.Add(placeholderCostumeListBox, 3, 1);

            pageUpDown = new NumericUpDown { Minimum = 0, Maximum = 65565, Value = Math.Max(0, VM.RosterPage_field), Dock = DockStyle.Fill };
            pageUpDown.ValueChanged += (s, e) => VM.RosterPage_field = (int)pageUpDown.Value;
            root.Controls.Add(pageUpDown, 0, 2);

            var saveBtn = new Button { Text = Loc("m_rosterEditorSave"), Dock = DockStyle.Fill };
            saveBtn.Click += (s, e) => { if (VM.SaveRosterCommand?.CanExecute(null) == true) VM.SaveRosterCommand.Execute(null); };
            var restoreBtn = new Button { Text = Loc("m_rosterEditorRestore"), Dock = DockStyle.Fill };
            restoreBtn.Click += (s, e) => { if (VM.RestoreRosterCommand?.CanExecute(null) == true) VM.RestoreRosterCommand.Execute(null); };
            root.Controls.Add(restoreBtn, 2, 2);
            root.Controls.Add(saveBtn, 3, 2);

            Controls.Add(root);
        }

        private ListBox MakeIconListBox(string name, ObservableCollection<CharacterSelectParamModel> source)
        {
            var lb = new ListBox
            {
                Name = name,
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 68,
                AllowDrop = true,
                DataSource = source,
            };
            lb.DrawItem += ListBox_DrawItem;
            lb.MouseDown += ListBox_MouseDown;
            lb.MouseMove += ListBox_MouseMove;
            lb.DragEnter += (s, e) => e.Effect = DragDropEffects.Move;
            lb.DragOver += (s, e) => e.Effect = DragDropEffects.Move;
            lb.DragDrop += ListBox_DragDrop;
            return lb;
        }

        // ---------------------------------------------------------------
        // Rendering: 64x64 icon + CSP code (bottom-left) + slot/costume index (bottom-right)
        // ---------------------------------------------------------------
        private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var lb = (ListBox)sender;
            var item = lb.Items[e.Index] as CharacterSelectParamModel;
            e.DrawBackground();

            if (item != null)
            {
                var img = item.CharacterIconPreview?.UnderlyingImage;
                if (img != null)
                    e.Graphics.DrawImage(img, new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 2, 64, 64));

                using (var brush = new SolidBrush(e.ForeColor))
                {
                    e.Graphics.DrawString(item.CSP_code, e.Font, brush, e.Bounds.Left + 70, e.Bounds.Top + 2);
                    string idx = lb.Name == "CharacterCostumeIconListPreview" || lb.Name == "PlaceholderCostumeIconListPreview"
                        ? item.CostumeIndex.ToString()
                        : item.SlotIndex.ToString();
                    e.Graphics.DrawString(idx, e.Font, brush, e.Bounds.Left + 70, e.Bounds.Top + 20);
                }
            }

            e.DrawFocusRectangle();
        }

        // ---------------------------------------------------------------
        // Drag start (mirrors the original's PreviewMouseMove hit-test, using
        // ListBox.IndexFromPoint instead of VisualTreeHelper.HitTest)
        // ---------------------------------------------------------------
        private void ListBox_MouseDown(object sender, MouseEventArgs e)
        {
            var lb = (ListBox)sender;
            dragSourceIndex = lb.IndexFromPoint(e.Location);
            dragSourceListName = lb.Name;
        }

        private void ListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragSourceIndex < 0) return;
            var lb = (ListBox)sender;
            if (lb.IndexFromPoint(e.Location) != dragSourceIndex) return; // still on the same item, wait for real drag
            var item = lb.Items[dragSourceIndex] as CharacterSelectParamModel;
            if (item == null) return;
            lb.DoDragDrop(item, DragDropEffects.Move);
        }

        // ---------------------------------------------------------------
        // Drop: same source/target/cross-list logic as the original
        // InstallMod-era LBoxSort_OnDrop, just using WinForms APIs to find
        // the target item.
        // ---------------------------------------------------------------
        private void ListBox_DragDrop(object sender, DragEventArgs e)
        {
            var targetListBox = (ListBox)sender;
            var receiverName = targetListBox.Name;
            var sourcePerson = e.Data.GetData(typeof(CharacterSelectParamModel)) as CharacterSelectParamModel;
            if (sourcePerson == null) return;

            var clientPoint = targetListBox.PointToClient(new Point(e.X, e.Y));
            int targetIndexInList = targetListBox.IndexFromPoint(clientPoint);

            int page = VM.RosterPage_field;

            string selectedCSP = null;
            int selectedSlot = -1;
            int selectedPage = page;
            if (VM.SelectedCharacterIndex >= 0 && VM.SelectedCharacterIndex < VM.CharacterList.Count)
            {
                selectedCSP = VM.CharacterList[VM.SelectedCharacterIndex].CSP_code;
                selectedSlot = VM.CharacterList[VM.SelectedCharacterIndex].SlotIndex;
                selectedPage = page;
            }
            else if (VM.SelectedPlaceholderCharacterIndex >= 0 && VM.SelectedPlaceholderCharacterIndex < VM.CharacterPlaceHolderList.Count)
            {
                selectedCSP = VM.CharacterPlaceHolderList[VM.SelectedPlaceholderCharacterIndex].CSP_code;
                selectedSlot = VM.CharacterPlaceHolderList[VM.SelectedPlaceholderCharacterIndex].SlotIndex;
                selectedPage = -1;
            }

            int sourceIndex = dragSourceListName switch
            {
                "CharacterIconListPreview" => VM.CharacterList.IndexOf(sourcePerson),
                "CharacterPlaceHolderIconListPreview" => VM.CharacterPlaceHolderList.IndexOf(sourcePerson),
                "PlaceholderCostumeIconListPreview" => VM.PlaceholderCostumePlaceHolderList.IndexOf(sourcePerson),
                "CharacterCostumeIconListPreview" => VM.CostumePlaceHolderList.IndexOf(sourcePerson),
                _ => -1
            };
            int sourceSlot = sourceIndex + 1;

            if (receiverName == dragSourceListName)
            {
                if (targetIndexInList < 0 || targetIndexInList >= targetListBox.Items.Count) return;
                var targetPerson = targetListBox.Items[targetIndexInList] as CharacterSelectParamModel;
                if (targetPerson == null || ReferenceEquals(targetPerson, sourcePerson)) return;

                int targetIndex = dragSourceListName switch
                {
                    "CharacterIconListPreview" => VM.CharacterList.IndexOf(targetPerson),
                    "CharacterPlaceHolderIconListPreview" => VM.CharacterPlaceHolderList.IndexOf(targetPerson),
                    "PlaceholderCostumeIconListPreview" => VM.PlaceholderCostumePlaceHolderList.IndexOf(targetPerson),
                    "CharacterCostumeIconListPreview" => VM.CostumePlaceHolderList.IndexOf(targetPerson),
                    _ => -1
                };
                int targetSlot = targetIndex + 1;

                if (dragSourceListName == "CharacterIconListPreview")
                    VM.ReplaceSlots(page, sourceSlot, page, targetSlot);
                else if (dragSourceListName == "CharacterPlaceHolderIconListPreview")
                    VM.ReplaceSlots(-1, sourceSlot, -1, targetSlot);
                else if (dragSourceListName == "PlaceholderCostumeIconListPreview")
                {
                    if (selectedPage == -1 && VM.SelectedPlaceholderCharacterIndex >= 0)
                        SwapCostumeIndices(-1, selectedSlot, sourcePerson.CostumeIndex, targetPerson.CostumeIndex);
                }
                else if (dragSourceListName == "CharacterCostumeIconListPreview")
                {
                    if (selectedPage >= 0 && VM.SelectedCharacterIndex >= 0)
                        SwapCostumeIndices(selectedPage, selectedSlot, sourcePerson.CostumeIndex, targetPerson.CostumeIndex);
                    else if (selectedPage == -1 && VM.SelectedPlaceholderCharacterIndex >= 0)
                        SwapCostumeIndices(-1, selectedSlot, sourcePerson.CostumeIndex, targetPerson.CostumeIndex);
                }
            }
            else
            {
                // Cross-list moves/conversions
                if (dragSourceListName == "CharacterIconListPreview")
                {
                    if (receiverName == "CharacterPlaceHolderIconListPreview")
                        MoveOnlyBaseCostume(page, sourceSlot, -1);
                    else if (receiverName == "PlaceholderCostumeIconListPreview" && VM.SelectedPlaceholderCharacterIndex >= 0)
                        VM.ReplaceSlots(page, sourceSlot, -1, VM.SelectedPlaceholderCharacterIndex + 1);
                    else if (receiverName == "CharacterCostumeIconListPreview")
                    {
                        if (VM.SelectedCharacterIndex >= 0)
                            VM.ReplaceSlots(page, sourceSlot, page, VM.SelectedCharacterIndex + 1);
                        else if (VM.SelectedPlaceholderCharacterIndex >= 0)
                            VM.ReplaceSlots(page, sourceSlot, -1, VM.SelectedPlaceholderCharacterIndex + 1);
                    }
                }
                else if (dragSourceListName == "CharacterPlaceHolderIconListPreview")
                {
                    if (receiverName == "CharacterIconListPreview")
                        MoveOnlyBaseCostume(-1, sourceSlot, page);
                    else if (receiverName == "PlaceholderCostumeIconListPreview" && VM.SelectedPlaceholderCharacterIndex >= 0)
                        VM.ReplaceSlots(-1, sourceSlot, -1, VM.SelectedPlaceholderCharacterIndex + 1);
                    else if (receiverName == "CharacterCostumeIconListPreview")
                    {
                        if (VM.SelectedCharacterIndex >= 0)
                            VM.ReplaceSlots(-1, sourceSlot, page, VM.SelectedCharacterIndex + 1);
                        else if (VM.SelectedPlaceholderCharacterIndex >= 0)
                            VM.ReplaceSlots(-1, sourceSlot, -1, VM.SelectedPlaceholderCharacterIndex + 1);
                    }
                }
                else if (dragSourceListName == "PlaceholderCostumeIconListPreview")
                {
                    if (receiverName == "CharacterIconListPreview")
                        VM.ConvertCostumeToSlot(sourcePerson.PageIndex, sourcePerson.SlotIndex, sourcePerson.CostumeIndex, page);
                    else if (receiverName == "CharacterPlaceHolderIconListPreview")
                        VM.ConvertCostumeToSlot(sourcePerson.PageIndex, sourcePerson.SlotIndex, sourcePerson.CostumeIndex, -1);
                    else if (receiverName == "CharacterCostumeIconListPreview")
                        MoveCostumeAcross(sourcePerson, VM.CostumePlaceHolderList, page);
                }
                else if (dragSourceListName == "CharacterCostumeIconListPreview")
                {
                    if (receiverName == "CharacterIconListPreview")
                        VM.ConvertCostumeToSlot(sourcePerson.PageIndex, sourcePerson.SlotIndex, sourcePerson.CostumeIndex, page);
                    else if (receiverName == "CharacterPlaceHolderIconListPreview")
                        VM.ConvertCostumeToSlot(sourcePerson.PageIndex, sourcePerson.SlotIndex, sourcePerson.CostumeIndex, -1);
                    else if (receiverName == "PlaceholderCostumeIconListPreview")
                        MoveCostumeAcross(sourcePerson, VM.PlaceholderCostumePlaceHolderList, -1);
                }
            }

            VM.RosterPage_field = 100;
            VM.RosterPage_field = page;

            if (selectedCSP != null && selectedSlot >= 0)
            {
                if (selectedPage >= 0)
                {
                    for (int i = 0; i < VM.CharacterList.Count; i++)
                        if (VM.CharacterList[i].CSP_code == selectedCSP && VM.CharacterList[i].SlotIndex == selectedSlot)
                        { VM.SelectedCharacterIndex = i; break; }
                }
                else
                {
                    for (int i = 0; i < VM.CharacterPlaceHolderList.Count; i++)
                        if (VM.CharacterPlaceHolderList[i].CSP_code == selectedCSP && VM.CharacterPlaceHolderList[i].SlotIndex == selectedSlot)
                        { VM.SelectedPlaceholderCharacterIndex = i; break; }
                }
            }

            dragSourceIndex = -1;
            dragSourceListName = null;
        }

        private void MoveOnlyBaseCostume(int sourcePage, int sourceSlot, int targetPage)
        {
            int freeSlot = VM.FreeSlotOnPage(targetPage);
            var entriesToMove = VM.CharacterFullList.Where(x => x.PageIndex == sourcePage && x.SlotIndex == sourceSlot).ToList();
            if (!entriesToMove.Any()) return;

            foreach (var entry in entriesToMove) { entry.PageIndex = targetPage; entry.SlotIndex = freeSlot; }
            foreach (var entry in VM.CharacterFullList)
                if (entry.PageIndex == sourcePage && entry.SlotIndex > sourceSlot) entry.SlotIndex--;

            VM.CharacterFullList = new ObservableCollection<CharacterSelectParamModel>(
                VM.CharacterFullList.OrderBy(x => x.PageIndex).ThenBy(x => x.SlotIndex).ThenBy(x => x.CostumeIndex));
        }

        private void SwapCostumeIndices(int page, int slot, int costume1, int costume2)
        {
            foreach (var entry in VM.CharacterFullList)
            {
                if (entry.PageIndex == page && entry.SlotIndex == slot)
                {
                    if (entry.CostumeIndex == costume1) entry.CostumeIndex = -999;
                    else if (entry.CostumeIndex == costume2) entry.CostumeIndex = costume1;
                }
            }
            foreach (var entry in VM.CharacterFullList)
                if (entry.PageIndex == page && entry.SlotIndex == slot && entry.CostumeIndex == -999)
                    entry.CostumeIndex = costume2;

            VM.CharacterFullList = new ObservableCollection<CharacterSelectParamModel>(
                VM.CharacterFullList.OrderBy(x => x.PageIndex).ThenBy(x => x.SlotIndex).ThenBy(x => x.CostumeIndex));
        }

        /// <summary>Shared tail-end of the two "move costume between the two costume
        /// lists" branches in the original code-behind (they were identical except
        /// for which target list's [0] entry supplies the target page/slot).</summary>
        private void MoveCostumeAcross(CharacterSelectParamModel sourcePerson, ObservableCollection<CharacterSelectParamModel> targetList, int fallbackPage)
        {
            int targetSlot = -1;
            int targetPage = fallbackPage;

            if (targetList != null && targetList.Count > 0)
            {
                targetPage = targetList[0].PageIndex;
                targetSlot = targetList[0].SlotIndex;
            }
            else if (VM.SelectedCharacterIndex >= 0 && VM.SelectedCharacterIndex < VM.CharacterList.Count)
            {
                targetSlot = VM.CharacterList[VM.SelectedCharacterIndex].SlotIndex;
            }
            else return;

            int maxCostume = 0;
            foreach (var e in VM.CharacterFullList)
                if (e.PageIndex == targetPage && e.SlotIndex == targetSlot && e.CostumeIndex > maxCostume)
                    maxCostume = e.CostumeIndex;

            foreach (var entry in VM.CharacterFullList)
            {
                if (entry.PageIndex == sourcePerson.PageIndex && entry.SlotIndex == sourcePerson.SlotIndex && entry.CostumeIndex == sourcePerson.CostumeIndex)
                {
                    entry.PageIndex = targetPage;
                    entry.SlotIndex = targetSlot;
                    entry.CostumeIndex = maxCostume + 1;
                    break;
                }
            }

            VM.CharacterFullList = new ObservableCollection<CharacterSelectParamModel>(
                VM.CharacterFullList.OrderBy(x => x.PageIndex).ThenBy(x => x.SlotIndex).ThenBy(x => x.CostumeIndex));
        }

        private static string Loc(string key)
        {
            var v = System.Windows.Application.Current.Resources[key];
            return v?.ToString() ?? key;
        }
    }
}
