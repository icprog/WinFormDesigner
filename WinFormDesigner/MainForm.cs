using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Xml;

using ICSharpCode.FormsDesigner;
using ICSharpCode.FormsDesigner.Services;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Reflection;
using System.Collections;
using System.IO.Ports;
using ICSharpCode.TextEditor;
using ICSharpCode.TextEditor.Document;
using DesignSurfaceExt;
using HelperClass;


namespace WinFormDesigner
{
    public partial class MainForm : Form
    {
        bool _ShouldUpdateSelectableObjects = false;
        bool inUpdate = false;

        IDesignerHost _host;
        PropertyGrid _propertyGrid;
        TextEditorControl _textEditor;

        ISelectionService _selectionService;
        CustomToolboxService _toolboxService;
        MenuCommandService _menuCommandService;

        Loader.CodeDomHostLoader _CodeDomHostLoader;
        private Form rootComponent = null;

        //DesignSurfaceExt.DesignSurfaceExt surface = new DesignSurfaceExt.DesignSurfaceExt();

        #region signleton

        private MainForm()
        {
            InitializeComponent();

            CustomInitialize();
        }
        static MainForm _form;
        public static MainForm Instance
        {
            get
            {
                if (null == _form)
                    _form = new MainForm();
                return _form;
            }
        }

        #endregion

        /// <summary>
        /// �����ʼ��
        /// </summary>
        void CustomInitialize()
        {
            #region ���ToolBox

            string fileName = Application.StartupPath + @"\..\data\SharpDevelopControlLibrary.xml";
            ComponentLibraryLoader componentLibraryLoader = new ComponentLibraryLoader();
            componentLibraryLoader.LoadToolComponentLibrary(fileName);

            Guanjinke.Windows.Forms.ToolBox toolBox = new Guanjinke.Windows.Forms.ToolBox { Dock = DockStyle.Fill };

            foreach (Category category in componentLibraryLoader.Categories)
            {
                if (category.IsEnabled)
                {
                    Guanjinke.Windows.Forms.ToolBoxCategory cate = new Guanjinke.Windows.Forms.ToolBoxCategory();
                    cate.ImageIndex = -1;
                    cate.IsOpen = false;
                    cate.Name = category.Name;
                    cate.Parent = null;

                    Guanjinke.Windows.Forms.ToolBoxItem item = new Guanjinke.Windows.Forms.ToolBoxItem();
                    item.Tag = null;
                    item.Name = "<Pointer>";
                    item.Parent = null;
                    cate.Items.Add(item);

                    foreach (ToolComponent component in category.ToolComponents)
                    {
                        item = new Guanjinke.Windows.Forms.ToolBoxItem();

                        System.Drawing.Design.ToolboxItem toolboxItem = new System.Drawing.Design.ToolboxItem();
                        toolboxItem.TypeName = component.FullName;
                        toolboxItem.Bitmap = componentLibraryLoader.GetIcon(component);
                        toolboxItem.DisplayName = component.Name;
                        Assembly asm = component.LoadAssembly();
                        toolboxItem.AssemblyName = asm.GetName();

                        item.Image = toolboxItem.Bitmap;
                        item.Tag = toolboxItem;
                        item.Name = component.Name;
                        item.Parent = null;

                        cate.Items.Add(item);
                    }

                    toolBox.Categories.Add(cate);
                }
            }

            pnlToolBox.Controls.Add(toolBox);//���panel��ӿؼ�

            #endregion

            #region ���PropertyPad

            _propertyGrid = new PropertyGrid { Dock = DockStyle.Fill };
            pnlPropertyGrid.Controls.Add(_propertyGrid);//�ұ����Ա�

            #endregion


            #region ��� "����" �� "�����" �� "Code"����

            ServiceContainer serviceContainer = new ServiceContainer();
            serviceContainer.AddService(typeof(System.ComponentModel.Design.IDesignerEventService), new DesignerEventService());
            serviceContainer.AddService(typeof(System.ComponentModel.Design.Serialization.INameCreationService), new NameCreationService());
            _toolboxService = new CustomToolboxService();
            serviceContainer.AddService(typeof(IToolboxService), _toolboxService);

            DesignSurface surface = new DesignSurface(serviceContainer);
            _host = (IDesignerHost)surface.GetService(typeof(IDesignerHost));

            serviceContainer.AddService(typeof(System.ComponentModel.Design.IEventBindingService), new ICSharpCode.FormsDesigner.Services.EventBindingService(surface));

            _menuCommandService = new MenuCommandService(surface);
            serviceContainer.AddService(typeof(IMenuCommandService), _menuCommandService);

            //surface.BeginLoad(typeof(Form));
            _CodeDomHostLoader = new Loader.CodeDomHostLoader();
            surface.BeginLoad(_CodeDomHostLoader);

            Control designerContorl = (Control)surface.View;


            designerContorl.BackColor = Color.Aqua;
            designerContorl.Dock = DockStyle.Fill;
            //��ȡroot���
            rootComponent = (Form)((IDesignerHost)this._host).RootComponent;

            rootComponent.FormBorderStyle = FormBorderStyle.None;


            #region ��ʼ�������С

            //- set the Size
            PropertyDescriptorCollection pdc = TypeDescriptor.GetProperties(designerContorl);
            //- Sets a PropertyDescriptor to the specific property.
            PropertyDescriptor pdS = pdc.Find("Size", false);
            if (null != pdS)
                pdS.SetValue(_host.RootComponent, new Size(800, 480));
            #endregion

            tpDesign.Controls.Add(designerContorl);//����


            _textEditor = new TextEditorControl
            {
                IsReadOnly = true,
                Dock = DockStyle.Fill,
                Document = { HighlightingStrategy = HighlightingStrategyFactory.CreateHighlightingStrategy("C#") }
            }; //����༭��
            tpCode.Controls.Add(_textEditor);

            #endregion

            _propertyGrid.SelectedObject = surface.ComponentContainer.Components[0];
            SetAlignMenuEnabled(false);


            _propertyGrid.Site = (new IDEContainer(_host)).CreateSite(_propertyGrid);
            _propertyGrid.PropertyTabs.AddTabType(typeof(System.Windows.Forms.Design.EventsTab), PropertyTabScope.Document);

            #region �¼���Ӧ

            // ѡ����ı�ʱ���¼�
            _selectionService = surface.GetService(typeof(ISelectionService)) as ISelectionService;//��ȡsurface�е�SelectionService
            _selectionService.SelectionChanged += new EventHandler(selectionService_SelectionChanged);

            // ��/ɾ/������������¼�
            IComponentChangeService componentChangeService = (IComponentChangeService)surface.GetService(typeof(IComponentChangeService));
            componentChangeService.ComponentAdded += ComponentListChanged;
            componentChangeService.ComponentRemoved += ComponentListChanged;
            componentChangeService.ComponentRename += ComponentListChanged;
            _host.TransactionClosed += new DesignerTransactionCloseEventHandler(TransactionClosed);

            // ѡ�в�ͬ�Ĺ�������Ŀ
            toolBox.SelectedItemChanged += delegate (object sender, Guanjinke.Windows.Forms.ToolBoxItem newItem)
            {
                _toolboxService.SetSelectedToolboxItem(newItem.Tag as System.Drawing.Design.ToolboxItem);
            };

            // ˫����������Ŀʱ���ӵ��������
            toolBox.ItemDoubleClicked += delegate (object sender, Guanjinke.Windows.Forms.ToolBoxItem newItem)
            {
                System.Drawing.Design.ToolboxItem toolboxItem = newItem.Tag as System.Drawing.Design.ToolboxItem;
                if (null != toolboxItem)
                {
                    IToolboxUser toolboxUser = _host.GetDesigner(_host.RootComponent as IComponent) as IToolboxUser;
                    if (null != toolboxUser)
                        toolboxUser.ToolPicked(toolboxItem);
                }

            };

            // �϶���������Ŀ��֧�ִ���
            toolBox.ItemDragStart += delegate (object sender, Guanjinke.Windows.Forms.ToolBoxItem newItem)
            {
                System.Drawing.Design.ToolboxItem toolboxItem = newItem.Tag as System.Drawing.Design.ToolboxItem;
                if (null != toolboxItem)
                {
                    DataObject dataObject = ((IToolboxService)_toolboxService).SerializeToolboxItem(toolboxItem) as DataObject;
                    toolBox.DoDragDrop(dataObject, DragDropEffects.Copy);
                }
            };

            _toolboxService.ResetToolboxItem += delegate
            {
                toolBox.ResetSelection();
            };


            #endregion

            tsmiDelete.ShortcutKeys = Keys.Delete;
            tsmiSelectAll.ShortcutKeys = Keys.Control | Keys.A;

            cmbControls.Sorted = true;
            cmbControls.DrawMode = DrawMode.OwnerDrawFixed;
            cmbControls.DrawItem += new DrawItemEventHandler(cmbControls_DrawItem);
            UpdateComboBox();

            #region ���ڼ���

            foreach (string com in System.IO.Ports.SerialPort.GetPortNames())
            {
                this.comPortName.Items.Add(com);
            }

            #endregion
        }

        void selectionService_SelectionChanged(object sender, EventArgs e)
        {
            object[] selection = new object[_selectionService.SelectionCount];
            _selectionService.GetSelectedComponents().CopyTo(selection, 0);
            _propertyGrid.SelectedObjects = selection;
            if (_selectionService.SelectionCount > 1)
                SetAlignMenuEnabled(true);
            else
                SetAlignMenuEnabled(false);
            SelectedObjectsChanged();
        }

        void ComponentListChanged(object sender, EventArgs e)
        {
            _ShouldUpdateSelectableObjects = true;
        }

        void TransactionClosed(object sender, DesignerTransactionCloseEventArgs e)
        {
            if (_ShouldUpdateSelectableObjects)
            {
                panel2.BeginInvoke(new MethodInvoker(UpdateComboBox));
                _ShouldUpdateSelectableObjects = false;
            }
        }

        #region ���¿ؼ�������
        /// <summary>
        /// ���¿ؼ�������
        /// </summary>
        void UpdateComboBox()
        {
            inUpdate = true;
            cmbControls.Items.Clear();
            ICollection collect = _host.Container.Components;
            if (null != collect)
            {
                foreach (object obj in collect)
                    cmbControls.Items.Add(obj);
            }

            selectionService_SelectionChanged(null, null);
            inUpdate = false;
        }
        #endregion
        void SelectedObjectsChanged()
        {
            inUpdate = true;
            object[] objs = _propertyGrid.SelectedObjects;
            if (null != objs && objs.Length == 1)
            {
                for (int i = 0; i < cmbControls.Items.Count; i++)
                    if (objs[0] == cmbControls.Items[i])
                    {
                        cmbControls.SelectedIndex = i;
                        break;
                    }
            }
            else
                cmbControls.SelectedIndex = -1;
            inUpdate = false;
        }

        #region �˵��¼�

        private void tsmiLeft_Click(object sender, EventArgs e)
        {
            _menuCommandService.GlobalInvoke(StandardCommands.AlignLeft);
        }

        private void tsmiCenter_Click(object sender, EventArgs e)
        {
            _menuCommandService.GlobalInvoke(StandardCommands.AlignVerticalCenters);
        }

        private void tsmiRight_Click(object sender, EventArgs e)
        {
            _menuCommandService.GlobalInvoke(StandardCommands.AlignRight);
        }

        private void tsmiTop_Click(object sender, EventArgs e)
        {
            _menuCommandService.GlobalInvoke(StandardCommands.AlignTop);
        }

        private void tsmiMiddle_Click(object sender, EventArgs e)
        {
            _menuCommandService.GlobalInvoke(StandardCommands.AlignHorizontalCenters);
        }

        private void tsmiBottom_Click(object sender, EventArgs e)
        {
            _menuCommandService.GlobalInvoke(StandardCommands.AlignBottom);
        }

        private void tsmiDelete_Click(object sender, EventArgs e)
        {
            _menuCommandService.GlobalInvoke(StandardCommands.Delete);
        }

        private void tsmiSelectAll_Click(object sender, EventArgs e)
        {
            int iCount = _host.Container.Components.Count;
            if (iCount <= 1)
                return;

            _menuCommandService.GlobalInvoke(StandardCommands.SelectAll);
        }

        void SetAlignMenuEnabled(bool bEnable)
        {
            tsmiLeft.Enabled = bEnable;
            tsmiCenter.Enabled = bEnable;
            tsmiRight.Enabled = bEnable;
            tsmiTop.Enabled = bEnable;
            tsmiMiddle.Enabled = bEnable;
            tsmiBottom.Enabled = bEnable;
        }

        private void tsmiRun_Click(object sender, EventArgs e)
        {
            _CodeDomHostLoader.Run();
        }

        #endregion

        private void cmbControls_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!inUpdate)
            {
                if (_selectionService != null)
                {
                    if (cmbControls.SelectedIndex >= 0)
                    {
                        _selectionService.SetSelectedComponents(new object[] { cmbControls.Items[cmbControls.SelectedIndex] });
                    }
                    else
                    {
                        _propertyGrid.SelectedObject = null;
                        _selectionService.SetSelectedComponents(new object[] { });
                    }
                }
            }
        }

        private void cmbControls_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= cmbControls.Items.Count)
            {
                return;
            }
            Graphics g = e.Graphics;
            Brush stringColor = SystemBrushes.ControlText;

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
                {
                    g.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                    stringColor = SystemBrushes.HighlightText;
                }
                else
                {
                    g.FillRectangle(SystemBrushes.Window, e.Bounds);
                }
            }
            else
            {
                g.FillRectangle(SystemBrushes.Window, e.Bounds);
            }

            object item = cmbControls.Items[e.Index];
            int xPos = e.Bounds.X;

            if (item is IComponent)
            {
                ISite site = ((IComponent)item).Site;
                if (site != null)
                {
                    string name = site.Name;
                    using (Font f = new Font(cmbControls.Font, FontStyle.Bold))
                    {
                        g.DrawString(name, f, stringColor, xPos, e.Bounds.Y);
                        xPos += (int)g.MeasureString(name + "-", f).Width;
                    }
                }
            }

            string typeString = item.GetType().ToString();
            g.DrawString(typeString, cmbControls.Font, stringColor, xPos, e.Bounds.Y);
        }

        #region ���ɺ�̨����
        private void tabContent_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabContent.SelectedIndex == 1)
            {
                _textEditor.Text = _CodeDomHostLoader.GetCode(Strings.CS);
            }
            else if (tabContent.SelectedIndex == 2)
            {

            }


        }
        #endregion

        #region ��̨�����ʲô�������Դ���
        private class Strings
        {
            public const string Design = "Design";
            public const string Code = "Code";
            public const string Xml = "Xml";
            public const string CS = "C#";
            public const string JS = "J#";
            public const string VB = "VB";
        }

        #endregion


        #region �򿪴���
        private void btnOpenCom_Click(object sender, EventArgs e)
        {

            try
            {
                if (this.serialPort1.IsOpen)
                {
                    this.serialPort1.Close();
                }
                else
                {
                    // ���ö˿ڲ���
                    this.serialPort1.BaudRate = int.Parse(this.comBaudRate.Text);
                    this.serialPort1.DataBits = int.Parse(this.comDataBit.Text);
                    this.serialPort1.StopBits = (StopBits)Enum.Parse(typeof(StopBits), this.comStopBit.Text);
                    this.serialPort1.Parity = (Parity)Enum.Parse(typeof(Parity), this.comParity.Text);
                    this.serialPort1.PortName = this.comPortName.Text;
                    //comport.Encoding = Encoding.ASCII;

                    //�򿪶˿�
                    this.serialPort1.Open();
                }
                this.groupBox1.Enabled = !this.serialPort1.IsOpen;
                //txtsend.Enabled = btnsend.Enabled = comport.IsOpen;

                if (this.serialPort1.IsOpen)
                {
                    this.btnOpenCom.Text = "&C�رն˿�";
                }
                else
                {
                    this.btnOpenCom.Text = "&O�򿪶˿�";
                }
                //if (this.serialPort1.IsOpen) txtsend.Focus();
            }
            catch (Exception er)
            {
                MessageBox.Show("�˿ڴ�ʧ�ܣ�" + er.Message, "��ʾ");
            }

        }
        #endregion

        #region ���ش��뵽������
        private void btnDownLoad_Click(object sender, EventArgs e)
        {
            #region ��ȡ�ؼ�

            if (null != rootComponent)
            {
                if (rootComponent.Controls.Count > 0)
                {
                    foreach (Control control in rootComponent.Controls)
                    {
                        ComponentGroup(control);
                    }
                }

                if (null!= btnPara&&btnPara.Length>0)
                {
                    if (this.serialPort1.IsOpen)
                    {
                        serialPort1.Write(btnPara, 0, btnPara.Length);
                    }
                    else
                    {
                        MessageBox.Show("�˿���δ��");
                    }
                }


            }

            #endregion


        }
        #endregion

        #region �ؼ�����,ת����byte����

        private List<byte[]> btnByteList;
        private byte [] btnPara=null;



        private void ComponentGroup(Control ctr)
        {

            #region Button

            if (ctr is Button)
            {
                btnByteList=new List<byte[]>();
                Button btn = ctr as Button;

                int wid = btn.Width;

                byte wid0 = Convert.ToByte((wid / 0x100) & 0xff);
                byte wid1 = Convert.ToByte(wid & 0xff); //��λ

                byte height0 = Convert.ToByte((btn.Height / 0x100) & 0xff);
                byte height1 = Convert.ToByte(btn.Height & 0xff); //��λ

                byte x0 = Convert.ToByte((btn.Location.X / 0x100) & 0xff);
                byte x1 = Convert.ToByte(btn.Location.X & 0xff); //��λ

                byte y0 = Convert.ToByte((btn.Location.Y / 0x100) & 0xff);
                byte y1 = Convert.ToByte(btn.Location.Y & 0xff); //��λ

                byte[] btnText = Encoding.GetEncoding("GB2312").GetBytes(btn.Text);

                byte colorR = Convert.ToByte(btn.ForeColor.R);
                byte colorG = Convert.ToByte(btn.ForeColor.G);
                byte colorB = Convert.ToByte(btn.ForeColor.B);
                byte[] foreColor = { colorR , colorG ,colorB };

                byte[] btnParaByte = { wid0, wid1, height0, height1, x0, x1, y0, y1 };
                
                btnByteList.Add(btnParaByte);
                btnByteList.Add(btnText);
                btnByteList.Add(foreColor);
                btnPara = ByteHelper.MergerArray(btnByteList);
                
            }
            if (ctr is TextBox)
            {

            }

            #endregion

        }



        #endregion


    }
}