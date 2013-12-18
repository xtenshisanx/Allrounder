using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Allrounder
{
    public partial class AllrounderSettings : Form
    {
        public AllrounderSettings()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            SkillTab tab = null;
            this.SkillSettingsBody.TabPages.RemoveAt(0);

            this.PotManaBox.Text = Settings.Instance.PotMana.ToString();
            this.PotHealthBox.Text = Settings.Instance.PotHealth.ToString();
            this.FightDistanceBox.Text = Settings.Instance.FightDistance.ToString();

            foreach(Skill skill in Settings.Instance.Skills)
            {
                tab = new SkillTab(skill.Name);
                foreach(System.Reflection.PropertyInfo info in typeof(Skill).GetProperties())
                {
                    if (info.GetValue(skill) != null && (info.PropertyType.ToString().Contains("Int32")) && (info.Name.Equals("MobsAroundTarget") || (Int32)info.GetValue(skill) != 0))
                    {
                        if (tab.Controls[info.Name + "Box"] != null)
                        {
                            if (info.Name.Equals("MobsAroundTarget"))
                            {
                                if ((Int32)info.GetValue(skill) == 1)
                                {
                                    tab.Controls[info.Name + "Box"].Text = "Me";
                                }
                                else
                                {
                                    tab.Controls[info.Name + "Box"].Text = "Maintarget";
                                }
                            }
                            else
                            {
                                tab.Controls[info.Name + "Box"].Text = info.GetValue(skill).ToString();
                            }
                            continue;
                        }
                    }
                    if (info.GetValue(skill) != null && (info.PropertyType.ToString().Contains("String") && ((String)info.GetValue(skill)) != null))
                    {
                        if (tab.Controls[info.Name + "Box"] != null)
                        {
                            tab.Controls[info.Name + "Box"].Text = info.GetValue(skill).ToString();
                            continue;
                        }
                    }
                    if(info.PropertyType.ToString().Contains("Boolean") && (Boolean)info.GetValue(skill))
                    {
                        CheckedListBox box = (CheckedListBox)tab.Controls["SkillTypeCheckBox"];
                        Int32 index = -1;
                        if(box != null)
                        {
                            index = box.Items.IndexOf(info.Name);
                            if(index == -1)
                            {
                                continue;
                            }
                            box.SetItemChecked(index, true);
                        }
                    }
                }
                this.SkillSettingsBody.TabPages.Add(tab);
                tab = null;
            }
        }
        private void SaveSettings()
        {
            if (!this.PotHealthBox.Text.Equals(""))
            {
                Settings.Instance.PotHealth = Convert.ToInt32(this.PotHealthBox.Text);
            }
            if (!this.PotManaBox.Text.Equals(""))
            {
                Settings.Instance.PotMana = Convert.ToInt32(this.PotManaBox.Text);
            }
            if (!this.FightDistanceBox.Text.Equals(""))
            {
                Settings.Instance.FightDistance = Convert.ToInt32(this.FightDistanceBox.Text);
            }
            Settings.Instance.Skills = new List<Skill>();
            foreach (TabPage page in SkillSettingsBody.TabPages)
            {
                if (page.Text != null)
                {
                    Skill temp = new Skill(page.Text);
                    foreach (Control _control in page.Controls)
                    {
                        if (_control.Name.ToLower().Contains("box") && !_control.Text.Equals("") && !_control.Name.Equals("SkillTypeCheckBox"))
                        {
                            if (typeof(Skill).GetProperty(_control.Name.Replace("Box", "")).PropertyType.ToString().Contains("Int32"))
                            {
                                Int32 tmp = 0;
                                if (_control.Name.Replace("Box", "").ToLower().Contains("aroundtarget"))
                                {
                                    if (_control.Text.ToLower().Equals("me"))
                                    {
                                        tmp = 1;
                                    }
                                    else
                                    {
                                        tmp = 0;
                                    }
                                }
                                else
                                {
                                    tmp = Int32.Parse(_control.Text);
                                }
                                typeof(Skill).GetProperty(_control.Name.Replace("Box", "")).SetValue(temp, tmp);
                            }
                            if (typeof(Skill).GetProperty(_control.Name.Replace("Box", "")).PropertyType.ToString().Contains("String") )
                            {
                                typeof(Skill).GetProperty(_control.Name.Replace("Box", "")).SetValue(temp, _control.Text);
                            }
                        }
                        if (_control.Name.Equals("SkillTypeCheckBox"))
                        {
                            foreach (object item in (_control as CheckedListBox).CheckedItems)
                            {
                                typeof(Skill).GetProperty(item.ToString()).SetValue(temp, true);
                            }
                        }
                    }
                    Settings.Instance.Skills.Add(temp);
                }
            }
            Settings.Instance.Save();
        }
        private void SaveButton_Click(object sender, EventArgs e)
        {
            SaveSettings();
            Close();
        }

        private void AddNewSkillButton_Click(object sender, EventArgs e)
        {
            this.SkillSettingsBody.TabPages.Add(new SkillTab(this.NewSkillNameBox.Text));
            this.SkillSettingsBody.SelectedTab = this.SkillSettingsBody.TabPages[this.NewSkillNameBox.Text];
            this.NewSkillNameBox.Text = "";
        }

        private void RemoveSkillButton_Click(object sender, EventArgs e)
        {
            this.SkillSettingsBody.TabPages.Remove(this.SkillSettingsBody.SelectedTab);
        }

        private void MoveSkillRightButton_Click(object sender, EventArgs e)
        {
            int CurrentPosition = this.SkillSettingsBody.TabPages.IndexOf(this.SkillSettingsBody.SelectedTab);
            
            if (CurrentPosition == null)
                return;

            TabPage Current = this.SkillSettingsBody.SelectedTab;
            TabPage Next = this.SkillSettingsBody.TabPages[CurrentPosition + 1];
            this.SkillSettingsBody.TabPages[CurrentPosition + 1] = Current;
            this.SkillSettingsBody.TabPages[CurrentPosition] = Next;
            this.SkillSettingsBody.SelectedTab = this.SkillSettingsBody.TabPages[CurrentPosition + 1];
            this.Update();
            return;
        }

        private void MoveSkillLeftButton_Click(object sender, EventArgs e)
        {

            int CurrentPosition = this.SkillSettingsBody.TabPages.IndexOf(this.SkillSettingsBody.SelectedTab);

            if (CurrentPosition == 0)
                return;

            TabPage Current = this.SkillSettingsBody.SelectedTab;
            TabPage Next = this.SkillSettingsBody.TabPages[CurrentPosition - 1];
            this.SkillSettingsBody.TabPages[CurrentPosition - 1] = Current;
            this.SkillSettingsBody.TabPages[CurrentPosition] = Next;
            this.SkillSettingsBody.SelectedTab = this.SkillSettingsBody.TabPages[CurrentPosition - 1];
            this.Update();
            return;
        }
    }
    public class SkillTab : TabPage
    {
        private System.Windows.Forms.Label GenericSkillSettingsLabel;
        private System.Windows.Forms.TextBox MinEnemyLifePercentBox;
        private System.Windows.Forms.TextBox MinLifePercentBox;
        private System.Windows.Forms.TextBox MinManaPercentBox;
        private System.Windows.Forms.Label MobsAroundTargetLabel;
        private System.Windows.Forms.Label MobsAroundCountLabel;
        private System.Windows.Forms.TextBox MobsAroundCountBox;
        private System.Windows.Forms.Label MobsAroundDistanceLabel;
        private System.Windows.Forms.TextBox MobsAroundDistanceBox;
        private System.Windows.Forms.Label MobsAroundCheckLabel;
        private System.Windows.Forms.Label MinEnemyLifePercentLabel;
        private System.Windows.Forms.Label MinLifePercentLabel;
        private System.Windows.Forms.Label MinManaPercentLabel;
        private System.Windows.Forms.ComboBox MobsAroundTargetBox;
        private System.Windows.Forms.Label EnemyInDistanceLabel;
        private System.Windows.Forms.TextBox EnemyInDistanceBox;
        private System.Windows.Forms.Label EnemyDistanceLabel;
        private System.Windows.Forms.TextBox EnemyDistanceBox;
        private System.Windows.Forms.Label MobDistanceCheckLabel;
        private System.Windows.Forms.ComboBox ChargeTypeBox;
        private System.Windows.Forms.Label MaxCountLabel;
        private System.Windows.Forms.TextBox MaxCountBox;
        private System.Windows.Forms.Label ChargeType;
        private System.Windows.Forms.Label ChargesLabel;
        private System.Windows.Forms.CheckedListBox SkillTypeCheckBox;
        private System.Windows.Forms.Label SkillTypeLabel;

        public SkillTab(string Skillname)
        {
            this.SkillTypeCheckBox = new System.Windows.Forms.CheckedListBox();
            this.SkillTypeLabel = new System.Windows.Forms.Label();
            this.ChargeTypeBox = new System.Windows.Forms.ComboBox();
            this.MaxCountLabel = new System.Windows.Forms.Label();
            this.MaxCountBox = new System.Windows.Forms.TextBox();
            this.ChargeType = new System.Windows.Forms.Label();
            this.ChargesLabel = new System.Windows.Forms.Label();
            this.EnemyInDistanceLabel = new System.Windows.Forms.Label();
            this.EnemyInDistanceBox = new System.Windows.Forms.TextBox();
            this.EnemyDistanceLabel = new System.Windows.Forms.Label();
            this.EnemyDistanceBox = new System.Windows.Forms.TextBox();
            this.MobDistanceCheckLabel = new System.Windows.Forms.Label();
            this.MobsAroundTargetBox = new System.Windows.Forms.ComboBox();
            this.MobsAroundTargetLabel = new System.Windows.Forms.Label();
            this.MobsAroundCountLabel = new System.Windows.Forms.Label();
            this.MobsAroundCountBox = new System.Windows.Forms.TextBox();
            this.MobsAroundDistanceLabel = new System.Windows.Forms.Label();
            this.MobsAroundDistanceBox = new System.Windows.Forms.TextBox();
            this.MobsAroundCheckLabel = new System.Windows.Forms.Label();
            this.MinEnemyLifePercentLabel = new System.Windows.Forms.Label();
            this.MinLifePercentLabel = new System.Windows.Forms.Label();
            this.MinManaPercentLabel = new System.Windows.Forms.Label();
            this.MinEnemyLifePercentBox = new System.Windows.Forms.TextBox();
            this.MinLifePercentBox = new System.Windows.Forms.TextBox();
            this.MinManaPercentBox = new System.Windows.Forms.TextBox();
            this.GenericSkillSettingsLabel = new System.Windows.Forms.Label();

            // 
            // SkillTypeCheckBox
            // 
            this.SkillTypeCheckBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SkillTypeCheckBox.FormattingEnabled = true;
            this.SkillTypeCheckBox.Items.AddRange(new object[] {
            "IsTrap",
            "IsSummon",
            "IsTotem",
            "IsCurse",
            "IsRanged",
            "GeneratesCharges",
            "KeepChargesUp"});
            this.SkillTypeCheckBox.Location = new System.Drawing.Point(207, 72);
            this.SkillTypeCheckBox.Name = "SkillTypeCheckBox";
            this.SkillTypeCheckBox.Size = new System.Drawing.Size(189, 123);
            this.SkillTypeCheckBox.TabIndex = 27;
            // 
            // SkillTypeLabel
            // 
            this.SkillTypeLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SkillTypeLabel.Location = new System.Drawing.Point(204, 46);
            this.SkillTypeLabel.Name = "SkillTypeLabel";
            this.SkillTypeLabel.Size = new System.Drawing.Size(192, 23);
            this.SkillTypeLabel.TabIndex = 26;
            this.SkillTypeLabel.Text = "Skill Type";
            this.SkillTypeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // ChargeTypeBox
            // 
            this.ChargeTypeBox.AutoCompleteCustomSource.AddRange(new string[] {
            "Power",
            "Frenzy",
            "Endurance"});
            this.ChargeTypeBox.FormattingEnabled = true;
            this.ChargeTypeBox.Items.AddRange(new object[] {
            "Power",
            "Frenzy",
            "Endurance"});
            this.ChargeTypeBox.Location = new System.Drawing.Point(296, 22);
            this.ChargeTypeBox.Name = "ChargeTypeBox";
            this.ChargeTypeBox.Size = new System.Drawing.Size(100, 21);
            this.ChargeTypeBox.TabIndex = 25;
            // 
            // MaxCountLabel
            // 
            this.MaxCountLabel.AutoSize = true;
            this.MaxCountLabel.Location = new System.Drawing.Point(4, 104);
            this.MaxCountLabel.Name = "MaxCountLabel";
            this.MaxCountLabel.Size = new System.Drawing.Size(55, 13);
            this.MaxCountLabel.TabIndex = 24;
            this.MaxCountLabel.Text = "MaxCount";
            // 
            // MaxCountBox
            // 
            this.MaxCountBox.Location = new System.Drawing.Point(98, 101);
            this.MaxCountBox.Name = "MaxCountBox";
            this.MaxCountBox.Size = new System.Drawing.Size(100, 20);
            this.MaxCountBox.TabIndex = 23;
            // 
            // ChargeType
            // 
            this.ChargeType.AutoSize = true;
            this.ChargeType.Location = new System.Drawing.Point(202, 26);
            this.ChargeType.Name = "ChargeType";
            this.ChargeType.Size = new System.Drawing.Size(65, 13);
            this.ChargeType.TabIndex = 22;
            this.ChargeType.Text = "ChargeType";
            // 
            // ChargesLabel
            // 
            this.ChargesLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ChargesLabel.Location = new System.Drawing.Point(204, 0);
            this.ChargesLabel.Name = "ChargesLabel";
            this.ChargesLabel.Size = new System.Drawing.Size(192, 23);
            this.ChargesLabel.TabIndex = 20;
            this.ChargesLabel.Text = "Charges";
            this.ChargesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // EnemyInDistanceLabel
            // 
            this.EnemyInDistanceLabel.AutoSize = true;
            this.EnemyInDistanceLabel.Location = new System.Drawing.Point(4, 284);
            this.EnemyInDistanceLabel.Name = "EnemyInDistanceLabel";
            this.EnemyInDistanceLabel.Size = new System.Drawing.Size(90, 13);
            this.EnemyInDistanceLabel.TabIndex = 19;
            this.EnemyInDistanceLabel.Text = "EnemyInDistance";
            // 
            // EnemyInDistanceBox
            // 
            this.EnemyInDistanceBox.Location = new System.Drawing.Point(100, 281);
            this.EnemyInDistanceBox.Name = "EnemyInDistanceBox";
            this.EnemyInDistanceBox.Size = new System.Drawing.Size(100, 20);
            this.EnemyInDistanceBox.TabIndex = 18;
            // 
            // EnemyDistanceLabel
            // 
            this.EnemyDistanceLabel.AutoSize = true;
            this.EnemyDistanceLabel.Location = new System.Drawing.Point(4, 258);
            this.EnemyDistanceLabel.Name = "EnemyDistanceLabel";
            this.EnemyDistanceLabel.Size = new System.Drawing.Size(81, 13);
            this.EnemyDistanceLabel.TabIndex = 17;
            this.EnemyDistanceLabel.Text = "EnemyDistance";
            // 
            // EnemyDistanceBox
            // 
            this.EnemyDistanceBox.Location = new System.Drawing.Point(98, 255);
            this.EnemyDistanceBox.Name = "EnemyDistanceBox";
            this.EnemyDistanceBox.Size = new System.Drawing.Size(100, 20);
            this.EnemyDistanceBox.TabIndex = 16;
            // 
            // MobDistanceCheckLabel
            // 
            this.MobDistanceCheckLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MobDistanceCheckLabel.Location = new System.Drawing.Point(6, 229);
            this.MobDistanceCheckLabel.Name = "MobDistanceCheckLabel";
            this.MobDistanceCheckLabel.Size = new System.Drawing.Size(192, 23);
            this.MobDistanceCheckLabel.TabIndex = 15;
            this.MobDistanceCheckLabel.Text = "Mob Distance Check";
            this.MobDistanceCheckLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MobsAroundTargetBox
            // 
            this.MobsAroundTargetBox.AutoCompleteCustomSource.AddRange(new string[] {
            "Maintarget",
            "Me"});
            this.MobsAroundTargetBox.FormattingEnabled = true;
            this.MobsAroundTargetBox.Items.AddRange(new object[] {
            "Maintarget",
            "Me"});
            this.MobsAroundTargetBox.Location = new System.Drawing.Point(98, 205);
            this.MobsAroundTargetBox.Name = "MobsAroundTargetBox";
            this.MobsAroundTargetBox.Size = new System.Drawing.Size(100, 21);
            this.MobsAroundTargetBox.TabIndex = 14;
            // 
            // MobsAroundTargetLabel
            // 
            this.MobsAroundTargetLabel.AutoSize = true;
            this.MobsAroundTargetLabel.Location = new System.Drawing.Point(4, 208);
            this.MobsAroundTargetLabel.Name = "MobsAroundTargetLabel";
            this.MobsAroundTargetLabel.Size = new System.Drawing.Size(38, 13);
            this.MobsAroundTargetLabel.TabIndex = 13;
            this.MobsAroundTargetLabel.Text = "Target";
            // 
            // MobsAroundCountLabel
            // 
            this.MobsAroundCountLabel.AutoSize = true;
            this.MobsAroundCountLabel.Location = new System.Drawing.Point(4, 179);
            this.MobsAroundCountLabel.Name = "MobsAroundCountLabel";
            this.MobsAroundCountLabel.Size = new System.Drawing.Size(35, 13);
            this.MobsAroundCountLabel.TabIndex = 11;
            this.MobsAroundCountLabel.Text = "Count";
            // 
            // MobsAroundCountBox
            // 
            this.MobsAroundCountBox.Location = new System.Drawing.Point(98, 176);
            this.MobsAroundCountBox.Name = "MobsAroundCountBox";
            this.MobsAroundCountBox.Size = new System.Drawing.Size(100, 20);
            this.MobsAroundCountBox.TabIndex = 10;
            // 
            // MobsAroundDistanceLabel
            // 
            this.MobsAroundDistanceLabel.AutoSize = true;
            this.MobsAroundDistanceLabel.Location = new System.Drawing.Point(4, 153);
            this.MobsAroundDistanceLabel.Name = "MobsAroundDistanceLabel";
            this.MobsAroundDistanceLabel.Size = new System.Drawing.Size(49, 13);
            this.MobsAroundDistanceLabel.TabIndex = 9;
            this.MobsAroundDistanceLabel.Text = "Distance";
            // 
            // MobsAroundDistanceBox
            // 
            this.MobsAroundDistanceBox.Location = new System.Drawing.Point(98, 150);
            this.MobsAroundDistanceBox.Name = "MobsAroundDistanceBox";
            this.MobsAroundDistanceBox.Size = new System.Drawing.Size(100, 20);
            this.MobsAroundDistanceBox.TabIndex = 8;
            // 
            // MobsAroundCheckLabel
            // 
            this.MobsAroundCheckLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MobsAroundCheckLabel.Location = new System.Drawing.Point(6, 124);
            this.MobsAroundCheckLabel.Name = "MobsAroundCheckLabel";
            this.MobsAroundCheckLabel.Size = new System.Drawing.Size(192, 23);
            this.MobsAroundCheckLabel.TabIndex = 7;
            this.MobsAroundCheckLabel.Text = "Mobs Around Check";
            this.MobsAroundCheckLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MinEnemyLifePercentLabel
            // 
            this.MinEnemyLifePercentLabel.AutoSize = true;
            this.MinEnemyLifePercentLabel.Location = new System.Drawing.Point(4, 78);
            this.MinEnemyLifePercentLabel.Name = "MinEnemyLifePercentLabel";
            this.MinEnemyLifePercentLabel.Size = new System.Drawing.Size(81, 13);
            this.MinEnemyLifePercentLabel.TabIndex = 6;
            this.MinEnemyLifePercentLabel.Text = "MinEnemyLife%";
            // 
            // MinLifePercentLabel
            // 
            this.MinLifePercentLabel.AutoSize = true;
            this.MinLifePercentLabel.Location = new System.Drawing.Point(4, 52);
            this.MinLifePercentLabel.Name = "MinLifePercentLabel";
            this.MinLifePercentLabel.Size = new System.Drawing.Size(49, 13);
            this.MinLifePercentLabel.TabIndex = 5;
            this.MinLifePercentLabel.Text = "MinLife%";
            // 
            // MinManaPercentLabel
            // 
            this.MinManaPercentLabel.AutoSize = true;
            this.MinManaPercentLabel.Location = new System.Drawing.Point(4, 26);
            this.MinManaPercentLabel.Name = "MinManaPercentLabel";
            this.MinManaPercentLabel.Size = new System.Drawing.Size(59, 13);
            this.MinManaPercentLabel.TabIndex = 4;
            this.MinManaPercentLabel.Text = "MinMana%";
            // 
            // MinEnemyLifePercentBox
            // 
            this.MinEnemyLifePercentBox.Location = new System.Drawing.Point(98, 75);
            this.MinEnemyLifePercentBox.Name = "MinEnemyLifePercentBox";
            this.MinEnemyLifePercentBox.Size = new System.Drawing.Size(100, 20);
            this.MinEnemyLifePercentBox.TabIndex = 3;
            // 
            // MinLifePercentBox
            // 
            this.MinLifePercentBox.Location = new System.Drawing.Point(98, 49);
            this.MinLifePercentBox.Name = "MinLifePercentBox";
            this.MinLifePercentBox.Size = new System.Drawing.Size(100, 20);
            this.MinLifePercentBox.TabIndex = 2;
            // 
            // MinManaPercentBox
            // 
            this.MinManaPercentBox.Location = new System.Drawing.Point(98, 23);
            this.MinManaPercentBox.Name = "MinManaPercentBox";
            this.MinManaPercentBox.Size = new System.Drawing.Size(100, 20);
            this.MinManaPercentBox.TabIndex = 1;
            // 
            // GenericSkillSettingsLabel
            // 
            this.GenericSkillSettingsLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.GenericSkillSettingsLabel.Location = new System.Drawing.Point(6, 0);
            this.GenericSkillSettingsLabel.Name = "GenericSkillSettingsLabel";
            this.GenericSkillSettingsLabel.Size = new System.Drawing.Size(192, 23);
            this.GenericSkillSettingsLabel.TabIndex = 0;
            this.GenericSkillSettingsLabel.Text = "Generic Skill Settings";
            this.GenericSkillSettingsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            this.Controls.Add(this.SkillTypeCheckBox);
            this.Controls.Add(this.SkillTypeLabel);
            this.Controls.Add(this.ChargeTypeBox);
            this.Controls.Add(this.MaxCountLabel);
            this.Controls.Add(this.MaxCountBox);
            this.Controls.Add(this.ChargeType);
            this.Controls.Add(this.ChargesLabel);
            this.Controls.Add(this.EnemyInDistanceLabel);
            this.Controls.Add(this.EnemyInDistanceBox);
            this.Controls.Add(this.EnemyDistanceLabel);
            this.Controls.Add(this.EnemyDistanceBox);
            this.Controls.Add(this.MobDistanceCheckLabel);
            this.Controls.Add(this.MobsAroundTargetBox);
            this.Controls.Add(this.MobsAroundTargetLabel);
            this.Controls.Add(this.MobsAroundCountLabel);
            this.Controls.Add(this.MobsAroundCountBox);
            this.Controls.Add(this.MobsAroundDistanceLabel);
            this.Controls.Add(this.MobsAroundDistanceBox);
            this.Controls.Add(this.MobsAroundCheckLabel);
            this.Controls.Add(this.MinEnemyLifePercentLabel);
            this.Controls.Add(this.MinLifePercentLabel);
            this.Controls.Add(this.MinManaPercentLabel);
            this.Controls.Add(this.MinEnemyLifePercentBox);
            this.Controls.Add(this.MinLifePercentBox);
            this.Controls.Add(this.MinManaPercentBox);
            this.Controls.Add(this.GenericSkillSettingsLabel);
            this.Location = new System.Drawing.Point(4, 22);
            this.Name = Skillname;
            this.Padding = new System.Windows.Forms.Padding(3);
            this.Size = new System.Drawing.Size(439, 375);
            this.TabIndex = 0;
            this.Text = Skillname;
            this.UseVisualStyleBackColor = true;
        }
    }
}
