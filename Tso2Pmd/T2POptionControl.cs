﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

using TDCG;
using TDCGUtils;

namespace Tso2Pmd
{
    public partial class T2POptionControl : UserControl
    {
        string file_name;

        TemplateList template_list;
        CorrespondTableList cortable_list = new CorrespondTableList();

        public T2POptionControl()
        {
            InitializeComponent();
        }

        public void Initialize(ref Viewer viewer, TemplateList template_list)
        {
            // 出力フォルダ設定
            radioButton1.Checked = true;
            tbFolder.Enabled = false;
            button_Folder.Enabled = false;

            // -----------------------------------------------------
            this.template_list = template_list;
            taikeiControl1.Initialize(ref viewer);
            physicsControl1.Initialize(template_list);

            cortable_list.Load();
            foreach (string name in cortable_list.NameList)
            {
                cbBoneStructure.Items.Add(name);
            }
        }

        private void button_Folder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                //最初に選択するフォルダを指定する
                if (tbFolder.Text != "")
                    fbd.SelectedPath = tbFolder.Text;

                if (fbd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                tbFolder.Text = fbd.SelectedPath;
            }
        }

        /// フォームからパラメータを得て設定します。
        public void SetupOptions(TransTso2Pmd t2p)
        {
            t2p.TextureFilePrefix = tbTextureFilePrefix.Text;
            t2p.UseSpheremap = cbUseSpheremap.Checked;
            t2p.UseEdge = cbUseEdge.Checked;
            t2p.UniqueMaterial = cbUniqueMaterial.Checked;

            physicsControl1.SetPhysFlag();
            t2p.TemplateList = template_list;
            t2p.CorTableList = cortable_list;

            t2p.UseOneBone = rbOneBone.Checked; 

            t2p.InputHeader(textBox_ModelName.Text, textBox_Comment.Text);
        }

        public void SetFileName(string file_name)
        {
            this.file_name = file_name;
        }

        // 出力フォルダを得る
        public string GetOutputFilePath()
        {
            if (radioButton1.Checked == true)
            {
                string outputFilePath = Path.Combine(
                    Path.GetDirectoryName(file_name),
                    Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file_name)));
                Directory.CreateDirectory(outputFilePath);
                return outputFilePath;
            }
            else if (radioButton2.Checked == true)
            {
                return System.IO.Path.GetDirectoryName(file_name);
            }
            else
            {
                return tbFolder.Text;
            }
        }

        public string GetModelName()
        {
            return textBox_ModelName.Text;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            tbFolder.Enabled = false;
            button_Folder.Enabled = false;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            tbFolder.Enabled = false;
            button_Folder.Enabled = false;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            tbFolder.Enabled = true;
            button_Folder.Enabled = true;
        }

        public void SetupTPOListRatio()
        {
            taikeiControl1.SetupTPOListRatio();
        }

        public void SaveTPOConfig(string path)
        {
            taikeiControl1.SaveTPOConfig(path);
        }

        private void radioButton_Bone1_CheckedChanged(object sender, EventArgs e)
        {
            cbBoneStructure.Enabled = false;
        }

        private void radioButton_Bone0_CheckedChanged(object sender, EventArgs e)
        {
            cbBoneStructure.Enabled = true;
        }

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            cortable_list.Selection[cbBoneStructure.Items[e.Index].ToString()] = (e.NewValue == CheckState.Checked);
        }
    }
}
