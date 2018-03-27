using Colso.Xrm.ChartTransferTool.AppCode;
using Colso.Xrm.ChartTransferTool.Forms;
using McTools.Xrm.Connection;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.ServiceModel;
using System.Windows.Forms;
using System.Xml;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;

namespace Colso.Xrm.ChartTransferTool
{
    public partial class ChartTransferTool : PluginControlBase, IXrmToolBoxPluginControl, IGitHubPlugin, IHelpPlugin, IStatusBarMessenger, IPayPalPlugin
    {
        #region Variables

        /// <summary>
        /// Information panel
        /// </summary>
        private Panel informationPanel;

        /// <summary>
        /// Dynamics CRM 2011 organization service
        /// </summary>
        private IOrganizationService service;

        /// <summary>
        /// Dynamics CRM 2011 target organization service
        /// </summary>
        private IOrganizationService targetService;

        private bool workingstate = false;
        private Dictionary<string, int> lvSortcolumns = new Dictionary<string, int>();

        #endregion Variables

        public ChartTransferTool()
        {
            InitializeComponent();
        }

        #region XrmToolbox

        //public event EventHandler OnCloseTool;
        //public event EventHandler OnRequestConnection;
        public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

        public Image PluginLogo
        {
            get { return null; }
        }

        public string HelpUrl
        {
            get
            {
                return "https://github.com/bcolpaert/Colso.Xrm.ChartTransferTool/wiki";
            }
        }

        public string RepositoryName
        {
            get
            {
                return "Colso.Xrm.ChartTransferTool";
            }
        }

        public string UserName
        {
            get
            {
                return "MscrmTools";
            }
        }

        public string DonationDescription
        {
            get
            {
                return "Donation for Chart Transfer Tool - XrmToolBox";
            }
        }

        public string EmailAccount
        {
            get
            {
                return "bramcolpaert@outlook.com";
            }
        }

        public void ClosingPlugin(PluginCloseInfo info)
        {
            if (info.FormReason != CloseReason.None ||
                info.ToolBoxReason == ToolBoxCloseReason.CloseAll ||
                info.ToolBoxReason == ToolBoxCloseReason.CloseAllExceptActive)
            {
                return;
            }

            info.Cancel = MessageBox.Show(@"Are you sure you want to close this tab?", @"Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes;
        }

        public void UpdateConnection(IOrganizationService newService, ConnectionDetail connectionDetail, string actionName = "", object parameter = null)
        {
            if (actionName == "TargetOrganization")
            {
                targetService = newService;
                SetConnectionLabel(connectionDetail, "Target");
            }
            else
            {
                service = newService;
                SetConnectionLabel(connectionDetail, "Source");
            }
        }

        public string GetCompany()
        {
            return GetType().GetCompany();
        }

        public string GetMyType()
        {
            return GetType().FullName;
        }

        public string GetVersion()
        {
            return GetType().Assembly.GetName().Version.ToString();
        }

        #endregion XrmToolbox

        #region Form events

        private void btnSelectTarget_Click(object sender, EventArgs e)
        {
            var args = new RequestConnectionEventArgs { ActionName = "TargetOrganization", Control = this };
            RaiseRequestConnectionEvent(args);
        }

        private void tsbCloseThisTab_Click(object sender, EventArgs e)
        {
            CloseTool();
        }

        private void tsbLoadEntities_Click(object sender, EventArgs e)
        {
            if (service == null)
            {
                var args = new RequestConnectionEventArgs { ActionName = "Load", Control = this };
                RaiseRequestConnectionEvent(args);
            }
            else
            {
                PopulateEntities();
            }
        }

        private void tsbTransferData_Click(object sender, EventArgs e)
        {
            Transfer();
        }

        private void lvEntities_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateCharts();
        }

        private void lvEntities_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            SetListViewSorting(lvEntities, e.Column);
        }

        private void lvAttributes_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            SetListViewSorting(lvCharts, e.Column);
        }

        private void chkAllAttributes_CheckedChanged(object sender, EventArgs e)
        {
            lvCharts.Items.OfType<ListViewItem>().ToList().ForEach(item => item.Checked = chkAllAttributes.Checked);
        }

        private void donateInUSDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDonationPage("USD");
        }

        private void donateInEURToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDonationPage("EUR");
        }

        private void donateInGBPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDonationPage("GBP");
        }

        #endregion Form events

        #region Methods

        private void SetConnectionLabel(ConnectionDetail detail, string serviceType)
        {
            switch (serviceType)
            {
                case "Source":
                    lbSourceValue.Text = detail.ConnectionName;
                    lbSourceValue.ForeColor = Color.Green;
                    break;

                case "Target":
                    lbTargetValue.Text = detail.ConnectionName;
                    lbTargetValue.ForeColor = Color.Green;
                    break;
            }
        }

        private void ManageWorkingState(bool working)
        {
            workingstate = working;
            gbEntities.Enabled = !working;
            gbCharts.Enabled = !working;
            Cursor = working ? Cursors.WaitCursor : Cursors.Default;
        }

        private void PopulateEntities()
        {
            if (!workingstate)
            {
                // Reinit other controls
                lvEntities.Items.Clear();
                lvCharts.Items.Clear();
                ManageWorkingState(true);

                informationPanel = InformationPanel.GetInformationPanel(this, "Loading entities...", 340, 150);

                // Launch treatment
                var bwFill = new BackgroundWorker();
                bwFill.DoWork += (sender, e) =>
                {
                    // Retrieve 
                    List<EntityMetadata> sourceList = MetadataHelper.RetrieveEntities(service);

                    // Prepare list of items
                    var sourceEntitiesList = new List<ListViewItem>();

                    foreach (EntityMetadata entity in sourceList)
                    {
                        var name = entity.DisplayName.UserLocalizedLabel == null ? string.Empty : entity.DisplayName.UserLocalizedLabel.Label;
                        var item = new ListViewItem(name);
                        item.Tag = entity;
                        item.SubItems.Add(entity.LogicalName);

                        if (!entity.IsCustomizable.Value)
                        {
                            item.ForeColor = Color.Gray;
                            item.ToolTipText = "This entity has not been defined as customizable";
                        }

                        sourceEntitiesList.Add(item);
                    }

                    e.Result = sourceEntitiesList;
                };
                bwFill.RunWorkerCompleted += (sender, e) =>
                {
                    informationPanel.Dispose();

                    if (e.Error != null)
                    {
                        MessageBox.Show(this, "An error occured: " + e.Error.Message, "Error", MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                    }
                    else
                    {
                        var items = (List<ListViewItem>)e.Result;
                        if (items.Count == 0)
                        {
                            MessageBox.Show(this, "The system does not contain any entities", "Warning", MessageBoxButtons.OK,
                                            MessageBoxIcon.Warning);
                        }
                        else
                        {
                            lvEntities.Items.AddRange(items.ToArray());
                        }
                    }

                    ManageWorkingState(false);
                };
                bwFill.RunWorkerAsync();
            }
        }

        private void PopulateCharts()
        {
            if (!workingstate)
            {
                // Reinit other controls
                lvCharts.Items.Clear();

                if (lvEntities.SelectedItems.Count > 0)
                {
                    var entityitem = lvEntities.SelectedItems[0];

                    if (entityitem != null && entityitem.Tag != null)
                    {
                        ManageWorkingState(true);

                        var entity = (EntityMetadata)entityitem.Tag;

                        // Launch treatment
                        var bwFill = new BackgroundWorker();
                        bwFill.DoWork += (sender, e) =>
                        {
                            // Retrieve 
                            var systemcharts = ChartHelper.RetrieveCharts(entity.LogicalName, service);
                            var usercharts = ChartHelper.RetrieveUserCharts(entity.LogicalName, service);
                            var charts = systemcharts.Concat(usercharts).OrderBy(c => c.GetAttributeValue<string>("name")).ToArray();

                            // Prepare list of items
                            var itemList = new List<ListViewItem>();

                            foreach (Entity chart in charts)
                            {
                                var name = chart.GetAttributeValue<string>("name");
                                var typename = chart.LogicalName == "savedqueryvisualization" ? "System" : "User";
                                var item = new ListViewItem(name);
                                item.Tag = chart;
                                item.ImageIndex = chart.LogicalName == "savedqueryvisualization" ? 0 : 1;
                                item.SubItems.Add(typename);
                                item.SubItems.Add(chart.GetAttributeValue<bool>("isdefault").ToString());

                                var iscustomizable = chart.GetAttributeValue<object>("iscustomizable");
                                var notcustomizable = iscustomizable is bool ? (bool)iscustomizable : (iscustomizable is BooleanManagedProperty ? ((BooleanManagedProperty) iscustomizable).Value : true);
                                if (notcustomizable)
                                {
                                    item.ForeColor = Color.Gray;
                                    item.ToolTipText = "This chart has not been defined as customizable";
                                }
                                itemList.Add(item);
                            }

                            e.Result = itemList;
                        };
                        bwFill.RunWorkerCompleted += (sender, e) =>
                        {
                            if (e.Error != null)
                            {
                                MessageBox.Show(this, "An error occured: " + e.Error.Message, "Error", MessageBoxButtons.OK,
                                                MessageBoxIcon.Error);
                            }
                            else
                            {
                                var items = (List<ListViewItem>)e.Result;
                                if (items.Count == 0)
                                {
                                    MessageBox.Show(this, "The entity does not contain any charts", "Warning", MessageBoxButtons.OK,
                                                    MessageBoxIcon.Warning);
                                }
                                else
                                {
                                    lvCharts.Items.AddRange(items.ToArray());
                                }
                            }

                            ManageWorkingState(false);
                        };
                        bwFill.RunWorkerAsync();
                    }
                }
            }
        }

        private void Transfer()
        {
            if (service == null || targetService == null)
            {
                MessageBox.Show("You must select both a source and a target organization", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (lvEntities.SelectedItems.Count > 0)
            {
                MessageBox.Show("You must select an entity", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (lvCharts.SelectedItems.Count == 0)
            {
                MessageBox.Show("You must select at least one chart to be transfered", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ManageWorkingState(true);

            informationPanel = InformationPanel.GetInformationPanel(this, "Transfering charts...", 340, 150);
            SendMessageToStatusBar(this, new StatusBarMessageEventArgs("Start transfering charts..."));

            var bwTransferData = new BackgroundWorker { WorkerReportsProgress = true };
            bwTransferData.DoWork += (sender, e) =>
            {
                var worker = (BackgroundWorker)sender;
                var entities = (List<Entity>)e.Argument;
                var errors = new List<Tuple<string, string>>();
                var entityitem = lvEntities.SelectedItems[0];
                    
                for (int i = 0; i < entities.Count; i++)
                {
                    var chart = entities[i];
                    var attributes = lvCharts.CheckedItems.Cast<ListViewItem>().Select(v => (Entity)v.Tag).ToList();

                    var name = chart.GetAttributeValue<string>("name");
                    worker.ReportProgress((i / entities.Count), string.Format("Transfering chart '{0}'...", name));

                    try
                    {
                        var entity = new AppCode.Chart(chart, service, targetService);
                        entity.Transfer();
                    }
                    catch (FaultException<OrganizationServiceFault> error)
                    {
                        if (error.HResult == -2146233087)
                        {
                            errors.Add(new Tuple<string, string>(name, "The record you tried to transfer already exists but you don't have read access to it. Get access to this record on the target organization to update it"));
                        }
                        else
                        {
                            errors.Add(new Tuple<string, string>(name, error.Message));
                        }
                    }
                }

                // Publish entity...
                if (entityitem != null && entityitem.Tag != null)
                    Publish(((Entity)entityitem.Tag).LogicalName);

                e.Result = errors;
            };
            bwTransferData.RunWorkerCompleted += (sender, e) =>
            {
                Controls.Remove(informationPanel);
                informationPanel.Dispose();
                //SendMessageToStatusBar(this, new StatusBarMessageEventArgs(string.Empty)); // keep showing transfer results afterwards
                ManageWorkingState(false);

                var errors = (List<Tuple<string, string>>)e.Result;

                if (errors.Count > 0)
                {
                    var errorDialog = new ErrorList((List<Tuple<string, string>>)e.Result);
                    errorDialog.ShowDialog(ParentForm);
                }
            };
            bwTransferData.ProgressChanged += (sender, e) =>
            {
                InformationPanel.ChangeInformationPanelMessage(informationPanel, e.UserState.ToString());
                SendMessageToStatusBar(this, new StatusBarMessageEventArgs(e.UserState.ToString()));
            };
            bwTransferData.RunWorkerAsync(lvCharts.CheckedItems.Cast<ListViewItem>().Select(v => (Entity)v.Tag).ToList());
        }

        private void Entity_OnStatusMessage(object sender, EventArgs e)
        {
            SendMessageToStatusBar(this, new StatusBarMessageEventArgs(((StatusMessageEventArgs)e).Message));
        }

        private void SetListViewSorting(ListView listview, int column)
        {
            int currentSortcolumn = -1;
            if (lvSortcolumns.ContainsKey(listview.Name))
                currentSortcolumn = lvSortcolumns[listview.Name];
            else
                lvSortcolumns.Add(listview.Name, currentSortcolumn);

            if (currentSortcolumn != column)
            {
                lvSortcolumns[listview.Name] = column;
                listview.Sorting = SortOrder.Ascending;
            }
            else
            {
                if (listview.Sorting == SortOrder.Ascending)
                    listview.Sorting = SortOrder.Descending;
                else
                    listview.Sorting = SortOrder.Ascending;
            }

            listview.ListViewItemSorter = new ListViewItemComparer(column, listview.Sorting);
        }

        private void Publish(string entity)
        {
            var pubRequest = new PublishXmlRequest();
            pubRequest.ParameterXml = string.Format(@"<importexportxml>
                                                           <entities>
                                                              <entity>{0}</entity>
                                                           </entities>
                                                        </importexportxml>",
                                                    entity);

            targetService.Execute(pubRequest);
        }

        private void OpenDonationPage(string currency)
        {
            var url = string.Format("https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business={0}&lc=GB&item_name={1}&currency_code={2}&no_note=0&bn=PP-DonationsBF:btn_donateCC_LG.gif:NonHostedGuest", EmailAccount, DonationDescription, currency);
            System.Diagnostics.Process.Start(url);
        }
        #endregion Methods

    }
}