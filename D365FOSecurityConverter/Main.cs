using D365FOSecurityConverter.Models;
using Equin.ApplicationFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace D365FOSecurityConverter
{
    public partial class Main : Form
    {
        static List<ParentToChildAssociation> parentToChildAssociations;

        public Main()
        {
            InitializeComponent();
        }

        #region EventListeners
        private void btnInputFileBrowse_Click(object sender, EventArgs e)
        {
            DialogResult result = inputFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                tb_inputFile.Text = inputFileDialog.FileName;
            }
        }

        private void btnOutputFolderBrowse_Click(object sender, EventArgs e)
        {
            DialogResult result = outputFolderDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                tb_outputFolder.Text = outputFolderDialog.SelectedPath;
            }
        }

        private void tbInputFile_TextChanged(object sender, EventArgs e)
        {
            btn_ExportToCode.Enabled = false;
            btn_ExportToUI.Enabled = false;
            btn_checkAll.Enabled = false;
            btn_UncheckAll.Enabled = false;
            if (tb_inputFile.Text == "")
                btn_Process.Enabled = false;
            else
                btn_Process.Enabled = true;
        }

        private void tbOutputFolder_TextChanged(object sender, EventArgs e)
        {
            if (tb_outputFolder.Text == "" || tb_inputFile.Text == "")
            {
                btn_ExportToCode.Enabled = false;
                btn_ExportToUI.Enabled = false;
                btn_checkAll.Enabled = false;
                btn_UncheckAll.Enabled = false;
            }

            if (tb_outputFolder.Text != "" && dgvSecurityLayers.Rows.Count > 0)
            {
                btn_ExportToCode.Enabled = true;
                btn_ExportToUI.Enabled = true;
                btn_checkAll.Enabled = true;
                btn_UncheckAll.Enabled = true;
            }

        }

        private void btnExportToCode_Click(object sender, EventArgs e)
        {
            try
            {
                if (FilePathCheck())
                {
                    ExportSecurityToCode(tb_inputFile.Text, tb_outputFolder.Text);
                    MessageBox.Show("Processing of security has completed successfully!", "Security File Processed Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
 
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Processing Security File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnProcess_Click(object sender, EventArgs e)
        {
            string inputFilePath = tb_inputFile.Text;
            parentToChildAssociations = new List<ParentToChildAssociation>();

            if (!File.Exists(inputFilePath))
            {
                MessageBox.Show("Input file does not exist", "Error Processing File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                try
                {
                    BindingListView<SecurityLayerGridObject> blv = new BindingListView<SecurityLayerGridObject>(ParseInputXML(inputFilePath));
                    parentToChildAssociations.AddRange(ProcessSecurityLayerAssociations(inputFilePath));
                    dgvSecurityLayers.DataSource = blv;
                    dgvSecurityLayers.Columns["OldName"].Visible = false;
                    dgvSecurityLayers.Columns["OldLabel"].Visible = false;
                    dgvSecurityLayers.Columns["OldDescription"].Visible = false;
                    dgvSecurityLayers.Columns["Type"].ReadOnly = true;
                    dgvSecurityLayers.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                    if (tb_outputFolder.Text != "")
                    {
                        btn_ExportToCode.Enabled = true;
                        btn_ExportToUI.Enabled = true;
                        btn_checkAll.Enabled = true;
                        btn_UncheckAll.Enabled = true;
                    }


                    dgvSecurityLayers.Columns["Name"].SortMode = DataGridViewColumnSortMode.Automatic;
                    dgvSecurityLayers.Columns["Label"].SortMode = DataGridViewColumnSortMode.Automatic;
                    dgvSecurityLayers.Columns["Type"].SortMode = DataGridViewColumnSortMode.Automatic;

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Processing Security File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }



        private void btnExportToUI_Click(object sender, EventArgs e)
        {
            try
            {
                if (FilePathCheck())
                {
                    ExportSecurityToUI(tb_inputFile.Text, tb_outputFolder.Text);
                    MessageBox.Show("Processing of security has completed successfully!", "Security File Processed Successfully", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Processing Security File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCheckAll_Click(object sender, EventArgs e)
        {
            int rowCount = dgvSecurityLayers.Rows.Count;
            for (int i = 0; i < rowCount; i++)
            {
                DataGridViewRow row = dgvSecurityLayers.Rows[i];
                row.Cells["Selected"].Value = true;
            }
        }

        private void btnUncheckAll_Click(object sender, EventArgs e)
        {
            int rowCount = dgvSecurityLayers.Rows.Count;
            for (int i = 0; i < rowCount; i++)
            {
                DataGridViewRow row = dgvSecurityLayers.Rows[i];
                row.Cells["Selected"].Value = false;
            }
        }

        private void dgvSecurityLayers_OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dgvSecurityLayers.Columns["Selected"].Index && e.RowIndex != -1)
            {
                DataGridViewRow selectedRow = dgvSecurityLayers.Rows[e.RowIndex];
                bool selected = (bool)selectedRow.Cells["Selected"].Value;
                if (selected)
                {
                    string name = (string)selectedRow.Cells["OldName"].Value;
                    string typeStr = (string)selectedRow.Cells["Type"].Value;
                    LayerType type;
                    Enum.TryParse(typeStr, out type);

                    List<SecurityLayer> ObjectsToSelect = new List<SecurityLayer>();
                    ProcessDependentSecurityElements(name, type, ObjectsToSelect);

                    int rowCount = dgvSecurityLayers.Rows.Count;
                    for (int i = 0; i < rowCount; i++)
                    {
                        DataGridViewRow row = dgvSecurityLayers.Rows[i];
                        string securityLayerName = (string)row.Cells["OldName"].Value;
                        string securityLayerTypeStr = (string)row.Cells["Type"].Value;
                        LayerType securityLayerType;
                        Enum.TryParse(securityLayerTypeStr, out securityLayerType);
                        if (ObjectsToSelect.Any(o =>
                             string.Equals(o.Name, securityLayerName, StringComparison.CurrentCultureIgnoreCase) && o.Type == securityLayerType))
                            row.Cells["Selected"].Value = true;
                    }
                }
            }
        }

        private void dgvSecurityLayers_OnCellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex == dgvSecurityLayers.Columns["Selected"].Index && e.RowIndex != -1)
            {
                dgvSecurityLayers.EndEdit();
            }
        }

        #endregion

        #region Exporters
        private void ExportSecurityToUI(string inputFilePath, string outputFilePath)
        {
            List<SecurityLayerGridObject> securityLayerList = ConvertGridToObjects();

            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(inputFilePath);

            string xml = xDoc.OuterXml;
            foreach (var securityLayer in securityLayerList.Where(sl => sl.Selected == true))
            {
                xml = ReplaceSecurityLayerParameters(xml, securityLayer);
            }

            IEnumerable<string> securityLayersToRemove = ConvertGridToObjects().Where(sl => sl.Selected == false).Select(x => x.Name);

            XmlDocument renamedXDoc = new XmlDocument();
            TextReader tr = new StringReader(xml);
            renamedXDoc.Load(tr);

            XmlNodeList roles = renamedXDoc.GetElementsByTagName("AxSecurityRole");
            List<XmlNode> rolesToRemove = new List<XmlNode>();
            foreach (XmlNode role in roles)
            {
                string roleName = role["Name"]?.InnerText;
                if (securityLayersToRemove.Contains(roleName))
                    rolesToRemove.Add(role);
            }

            foreach(XmlNode role in rolesToRemove)
                role.ParentNode.RemoveChild(role);

            XmlNodeList duties = renamedXDoc.GetElementsByTagName("AxSecurityDuty");
            List<XmlNode> dutiesToRemove = new List<XmlNode>();
            foreach (XmlNode duty in duties)
            {
                string dutyName = duty["Name"]?.InnerText;
                if (securityLayersToRemove.Contains(dutyName))
                    dutiesToRemove.Add(duty);
            }

            foreach (XmlNode duty in dutiesToRemove)
                duty.ParentNode.RemoveChild(duty);

            XmlNodeList privileges = renamedXDoc.GetElementsByTagName("AxSecurityPrivilege");
            List<XmlNode> privsToRemove = new List<XmlNode>();
            foreach (XmlNode privilege in privileges)
            {
                string privilegeName = privilege["Name"]?.InnerText;
                if (securityLayersToRemove.Contains(privilegeName))
                    privsToRemove.Add(privilege);
            }

            foreach (XmlNode priv in privsToRemove)
                priv.ParentNode.RemoveChild(priv);

            renamedXDoc.Save(outputFilePath + @"\SecurityDatabaseCustomizations.xml");
        }

        private void ExportSecurityToCode(string inputFilePath, string outputFolderPath)
        {
            List<SecurityLayerGridObject> securityLayerList = ConvertGridToObjects();
            string rootFolderPath = outputFolderPath + @"\D365FOCustomizedSecurity";
            string roleFolderPath = rootFolderPath + @"\AxSecurityRole";
            string dutyFolderPath = rootFolderPath + @"\AxSecurityDuty";
            string privFolderPath = rootFolderPath + @"\AxSecurityPrivilege";

            Directory.CreateDirectory(rootFolderPath);
            Directory.CreateDirectory(roleFolderPath);
            Directory.CreateDirectory(dutyFolderPath);
            Directory.CreateDirectory(privFolderPath);

            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(inputFilePath);

            string xml = xDoc.OuterXml;
            foreach (var securityLayer in securityLayerList)
            {
                xml = ReplaceSecurityLayerParameters(xml, securityLayer);
            }

            XmlDocument renamedXDoc = new XmlDocument();
            TextReader tr = new StringReader(xml);
            renamedXDoc.Load(tr);

            IEnumerable<string> selectedRoles = securityLayerList.Where(sl => sl.Selected == true && sl.Type == "Role").Select(x => x.Name);
            IEnumerable<string> selectedDuties = securityLayerList.Where(sl => sl.Selected == true && sl.Type == "Duty").Select(x => x.Name);
            IEnumerable<string> selectedPrivs = securityLayerList.Where(sl => sl.Selected == true && sl.Type == "Privilege").Select(x => x.Name);

            XmlNodeList roles = renamedXDoc.GetElementsByTagName("AxSecurityRole");
            foreach (XmlNode role in roles)
            {
                string roleName = role["Name"]?.InnerText;
                if (selectedRoles.Contains(roleName))
                {
                    string fileName = roleFolderPath + @"\" + roleName + @".xml";
                    File.WriteAllText(fileName, role.OuterXml);
                }
            }

            XmlNodeList duties = renamedXDoc.GetElementsByTagName("AxSecurityDuty");
            foreach (XmlNode duty in duties)
            {
                string dutyName = duty["Name"]?.InnerText;
                if (selectedDuties.Contains(dutyName))
                {
                    string fileName = dutyFolderPath + @"\" + dutyName + @".xml";
                    File.WriteAllText(fileName, duty.OuterXml);
                }
            }

            XmlNodeList privileges = renamedXDoc.GetElementsByTagName("AxSecurityPrivilege");
            foreach (XmlNode privilege in privileges)
            {
                string privilegeName = privilege["Name"]?.InnerText;
                if (selectedPrivs.Contains(privilegeName))
                {
                    string fileName = privFolderPath + @"\" + privilegeName + @".xml";
                    File.WriteAllText(fileName, privilege.OuterXml);
                }
            }
        }

        #endregion

        #region HelperMethods

        private List<SecurityLayerGridObject> ConvertGridToObjects()
        {
            List<SecurityLayerGridObject> securityLayers = new List<SecurityLayerGridObject>();
            int count = dgvSecurityLayers.RowCount;
            for (int index = 0; index < count; index++)
            {
                DataGridViewRow row = dgvSecurityLayers.Rows[index];
                SecurityLayerGridObject sl = new SecurityLayerGridObject()
                {
                    Selected = (bool)row.Cells["Selected"].Value,
                    OldName = (string)row.Cells["OldName"].Value,
                    Name = (string)row.Cells["Name"].Value,
                    OldLabel = (string)row.Cells["OldLabel"].Value,
                    Label = (string)row.Cells["Label"].Value,
                    OldDescription = (string)row.Cells["OldDescription"].Value,
                    Description = (string)row.Cells["Description"].Value,
                    Type = (string)row.Cells["Type"].Value
                };
                securityLayers.Add(sl);
            }
            return securityLayers;
        }

        private string ReplaceSecurityLayerParameters(string xml, SecurityLayerGridObject securityLayer)
        {
            if (securityLayer.OldName != securityLayer.Name)
            {
                xml = xml.Replace("<Name>" + securityLayer.OldName + "</Name>", "<Name>" + securityLayer.Name + "</Name>");
            }
            if (securityLayer.OldLabel != securityLayer.Label)
            {
                xml = xml.Replace("<Label>" + securityLayer.OldLabel + "</Label>", "<Label>" + securityLayer.Label + "</Label>");
            }
            if (securityLayer.OldDescription != securityLayer.Description)
            {
                xml = xml.Replace("<Description>" + securityLayer.OldDescription + "</Description>", "<Description>" + securityLayer.Description + "</Description>");
            }
            return xml;
        }

        private List<ParentToChildAssociation> ProcessSecurityLayerAssociations(string inputFilePath)
        {
            List<ParentToChildAssociation> securityLayerAssociations = new List<ParentToChildAssociation>();
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(inputFilePath);

            XmlNodeList roles = xDoc.GetElementsByTagName("AxSecurityRole");
            foreach (XmlNode role in roles)
            {
                XmlNodeList roleSubRoles = role.SelectNodes("SubRoles//AxSecurityRoleReference");
                foreach (XmlNode roleSubRole in roleSubRoles)
                {
                    ParentToChildAssociation pca = new ParentToChildAssociation();
                    pca.ParentSystemName = role["Name"]?.InnerText;
                    pca.ParentType = LayerType.Role;
                    pca.ChildSystemName = roleSubRole["Name"]?.InnerText;
                    pca.ChildType = LayerType.Role;
                    securityLayerAssociations.Add(pca);
                }

                XmlNodeList roleDuties = role.SelectNodes("Duties//AxSecurityDutyReference");
                foreach (XmlNode roleDuty in roleDuties)
                {
                    ParentToChildAssociation pca = new ParentToChildAssociation();
                    pca.ParentSystemName = role["Name"]?.InnerText;
                    pca.ParentType = LayerType.Role;
                    pca.ChildSystemName = roleDuty["Name"]?.InnerText;
                    pca.ChildType = LayerType.Duty;
                    securityLayerAssociations.Add(pca);
                }

                XmlNodeList rolePrivs = role.SelectNodes("Privileges//AxSecurityPrivilegeReference");
                foreach (XmlNode rolePriv in rolePrivs)
                {
                    ParentToChildAssociation pca = new ParentToChildAssociation();
                    pca.ParentSystemName = role["Name"]?.InnerText;
                    pca.ParentType = LayerType.Role;
                    pca.ChildSystemName = rolePriv["Name"]?.InnerText;
                    pca.ChildType = LayerType.Privilege;
                    securityLayerAssociations.Add(pca);
                }
            }

            XmlNodeList duties = xDoc.GetElementsByTagName("AxSecurityDuty");
            foreach (XmlNode duty in duties)
            {
                XmlNodeList dutyPrivs = duty.SelectNodes("Privileges//AxSecurityPrivilegeReference");
                foreach (XmlNode dutyPriv in dutyPrivs)
                {
                    ParentToChildAssociation pca = new ParentToChildAssociation();
                    pca.ParentSystemName = duty["Name"]?.InnerText;
                    pca.ParentType = LayerType.Duty;
                    pca.ChildSystemName = dutyPriv["Name"]?.InnerText;
                    pca.ChildType = LayerType.Privilege;
                    securityLayerAssociations.Add(pca);
                }
            }

            return securityLayerAssociations;
        }

        private bool FilePathCheck()
        {
            string inputFilePath = tb_inputFile.Text;
            string outputFolderPath = tb_outputFolder.Text;

            if (!File.Exists(inputFilePath))
            {
                MessageBox.Show("Input file does not exist", "Error Processing File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (!Directory.Exists(outputFolderPath))
            {
                MessageBox.Show("Output folder path does not exist", "Error Processing File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return true;
        }

        private void ProcessDependentSecurityElements(string name, LayerType type, List<SecurityLayer> objectList)
        {
            objectList.Add(new SecurityLayer()
            {
                Name = name,
                Type = type
            });

            IEnumerable<ParentToChildAssociation> dependentObjects = parentToChildAssociations.Where(pca =>
                string.Equals(pca.ParentSystemName, name, StringComparison.CurrentCultureIgnoreCase) &&
                pca.ParentType == type);

            foreach (var dependentObject in dependentObjects)
                ProcessDependentSecurityElements(dependentObject.ChildSystemName, dependentObject.ChildType, objectList);
        }

        #endregion

        #region XML
        private List<SecurityLayerGridObject> ParseInputXML(string inputFilePath)
        {
            List<SecurityLayerGridObject> securityLayerList = new List<SecurityLayerGridObject>();
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(inputFilePath);
            XmlNodeList roles = xDoc.GetElementsByTagName("AxSecurityRole");
            foreach (XmlNode role in roles)
            {
                string roleName = role["Name"]?.InnerText;
                if (roleName != null)
                {
                    SecurityLayerGridObject sl = new SecurityLayerGridObject
                    {
                        Selected = false,
                        OldName = roleName,
                        Name = roleName,
                        OldLabel = role["Label"]?.InnerText ?? "",
                        Label = role["Label"]?.InnerText ?? "",
                        OldDescription = role["Description"]?.InnerText ?? "",
                        Description = role["Description"]?.InnerText ?? "",
                        Type = "Role"
                    };
                    securityLayerList.Add(sl);
                }
            }

            XmlNodeList duties = xDoc.GetElementsByTagName("AxSecurityDuty");
            foreach (XmlNode duty in duties)
            {
                string dutyName = duty["Name"]?.InnerText;
                if (dutyName != null)
                {
                    SecurityLayerGridObject sl = new SecurityLayerGridObject
                    {
                        Selected = false,
                        OldName = dutyName,
                        Name = dutyName,
                        OldLabel = duty["Label"]?.InnerText ?? "",
                        Label = duty["Label"]?.InnerText ?? "",
                        OldDescription = duty["Description"]?.InnerText ?? "",
                        Description = duty["Description"]?.InnerText ?? "",
                        Type = "Duty"
                    };
                    securityLayerList.Add(sl);
                }
            }

            XmlNodeList privileges = xDoc.GetElementsByTagName("AxSecurityPrivilege");
            foreach (XmlNode privilege in privileges)
            {
                string privilegeName = privilege["Name"]?.InnerText;
                if (privilegeName != null)
                {
                    SecurityLayerGridObject sl = new SecurityLayerGridObject
                    {
                        Selected = false,
                        OldName = privilegeName,
                        Name = privilegeName,
                        OldLabel = privilege["Label"]?.InnerText ?? "",
                        Label = privilege["Label"]?.InnerText ?? "",
                        OldDescription = privilege["Description"]?.InnerText ?? "",
                        Description = privilege["Description"]?.InnerText ?? "",
                        Type = "Privilege"
                    };
                    securityLayerList.Add(sl);
                }
            }
            return securityLayerList;
        }
        #endregion
    }
}