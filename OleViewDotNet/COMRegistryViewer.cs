﻿//    This file is part of OleViewDotNet.
//
//    OleViewDotNet is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    OleViewDotNet is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with OleViewDotNet.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using WeifenLuo.WinFormsUI.Docking;
using System.Text.RegularExpressions;

namespace OleViewDotNet
{
    /// <summary>
    /// Form to view the COM registration information
    /// </summary>
    public partial class COMRegistryViewer : DockContent
    {
        /// <summary>
        /// Current registry
        /// </summary>
        COMRegistry m_reg;

        TreeNode[] m_originalNodes;

        /// <summary>
        /// Enumeration to indicate what to display
        /// </summary>
        public enum DisplayMode
        {
            CLSIDs,
            ProgIDs,
            CLSIDsByName,
            CLSIDsByServer,
            CLSIDsByLocalServer,
            Interfaces,
            InterfacesByName,
            ImplementedCategories,
            PreApproved,
            IELowRights,
        }        

        /// <summary>
        /// Current display mode
        /// </summary>
        private DisplayMode m_mode;

        /// <summary>
        /// Constants for the ImageList icons
        /// </summary>
        private const int FolderIcon = 0;
        private const int InterfaceIcon = 1;
        private const int ClassIcon = 2;        

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="reg">The COM registry</param>
        /// <param name="mode">The display mode</param>
        public COMRegistryViewer(COMRegistry reg, DisplayMode mode)
        {
            InitializeComponent();
            m_reg = reg;
            m_mode = mode;
            comboBoxMode.SelectedIndex = 0;
            SetupTree();
        }

        private void SetupTree()
        {
            Cursor currCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                switch (m_mode)
                {
                    case DisplayMode.CLSIDsByName:
                        LoadCLSIDsByNames();
                        break;
                    case DisplayMode.CLSIDs:
                        LoadCLSIDs();
                        break;
                    case DisplayMode.ProgIDs:
                        LoadProgIDs();
                        break;
                    case DisplayMode.CLSIDsByServer:
                        LoadCLSIDByServer(false);
                        break;
                    case DisplayMode.CLSIDsByLocalServer:
                        LoadCLSIDByServer(true);
                        break;
                    case DisplayMode.Interfaces:
                        LoadInterfaces();
                        break;
                    case DisplayMode.InterfacesByName:
                        LoadInterfacesByName();
                        break;
                    case DisplayMode.ImplementedCategories:
                        LoadImplementedCategories();
                        break;
                    case DisplayMode.PreApproved:
                        LoadPreApproved();
                        break;
                    case DisplayMode.IELowRights:
                        LoadIELowRights();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Cursor.Current = currCursor;
            m_originalNodes = treeComRegistry.Nodes.Cast<TreeNode>().ToArray();
        }

        /// <summary>
        /// Build a tooltip for a CLSID entry
        /// </summary>
        /// <param name="ent">The CLSID entry to build the tool tip from</param>
        /// <returns>A string tooltip</returns>
        private string BuildCLSIDToolTip(COMCLSIDEntry ent)
        {
            StringBuilder strRet = new StringBuilder();

            strRet.AppendFormat("CLSID: {0}\n", ent.Clsid.ToString("B"));
            strRet.AppendFormat("Name: {0}\n", ent.Name);
            strRet.AppendFormat("{0}: {1}\n", ent.Type.ToString(), ent.Server);
            if (ent.Server != ent.CmdLine)
            {
                strRet.AppendFormat("Cmommand Line: {0}\n", ent.CmdLine);
            }
            string[] progids = ent.ProgIDs;
            if (progids.Length > 0)
            {
                strRet.Append("ProgIDs:\n");
                foreach (string progid in progids)
                {
                    strRet.AppendFormat("{0}\n", progid);
                }
            }
            if (ent.AppID != Guid.Empty)
            {
                strRet.AppendFormat("AppID: {0}\n", ent.AppID.ToString("B"));            
            }
            if (ent.TypeLib != Guid.Empty)
            {
                strRet.AppendFormat("TypeLib: {0}\n", ent.TypeLib.ToString("B"));
            }
            
            COMInterfaceEntry[] proxies = ent.Proxies;
            if (proxies.Length > 0)
            {
                strRet.Append("Interface Proxies:\n");
                foreach (COMInterfaceEntry intEnt in proxies)
                {
                    strRet.AppendFormat("{0} - {1}\n", intEnt.Iid.ToString(), intEnt.Name);
                }
            } 

            return strRet.ToString();
        }

        /// <summary>
        /// Build a ProgID entry tooltip
        /// </summary>
        /// <param name="ent">The ProgID entry</param>
        /// <returns>The ProgID tooltip</returns>
        private string BuildProgIDToolTip(COMProgIDEntry ent)
        {
            string strRet;
            if (ent.Entry != null)
            {
                strRet = BuildCLSIDToolTip(ent.Entry);
            }
            else
            {
                strRet = String.Format("CLSID: {0}\n", ent.Clsid.ToString("B"));
            }

            return strRet;
        }

        private string BuildInterfaceToolTip(COMInterfaceEntry ent)
        {
            string strRet;

            strRet = String.Format("Name: {0}\n", ent.Name);
            strRet += String.Format("IID: {0}\n", ent.Iid.ToString("B"));
            if (ent.ProxyClsid != Guid.Empty)
            {
                strRet += String.Format("ProxyCLSID: {0}\n", ent.ProxyClsid.ToString("B"));
            }

            return strRet;
        }

        private TreeNode CreateCLSIDNode(COMCLSIDEntry ent)
        {            
            TreeNode nodeRet = new TreeNode(String.Format("{0} - {1}", ent.Clsid.ToString(), ent.Name), ClassIcon, ClassIcon);
            nodeRet.ToolTipText = BuildCLSIDToolTip(ent);
            nodeRet.Tag = ent;
            nodeRet.Nodes.Add("IUnknown");

            return nodeRet;
        }

        private TreeNode CreateInterfaceNode(COMInterfaceEntry ent)
        {
            TreeNode nodeRet = new TreeNode(String.Format("{0} - {1}", ent.Iid.ToString(), ent.Name), InterfaceIcon, InterfaceIcon);
            nodeRet.ToolTipText = BuildInterfaceToolTip(ent);
            nodeRet.Tag = ent;

            return nodeRet;
        }

        private TreeNode CreateInterfaceNameNode(COMInterfaceEntry ent)
        {
            TreeNode nodeRet = new TreeNode(ent.Name, InterfaceIcon, InterfaceIcon);
            nodeRet.ToolTipText = BuildInterfaceToolTip(ent);
            nodeRet.Tag = ent;

            return nodeRet;
        }

        private void LoadCLSIDs()
        {
            int i = 0;
            TreeNode[] clsidNodes = new TreeNode[m_reg.Clsids.Count];
            foreach (COMCLSIDEntry ent in m_reg.Clsids.Values)
            {
                clsidNodes[i] = CreateCLSIDNode(ent);                
                i++;
            }
            
            treeComRegistry.Nodes.AddRange(clsidNodes);
            TabText = "CLSIDs";            
        }

        private void LoadProgIDs()
        {
            int i = 0;
            TreeNode[] progidNodes = new TreeNode[m_reg.Progids.Count];
            foreach (COMProgIDEntry ent in m_reg.Progids.Values)
            {
                progidNodes[i] = new TreeNode(ent.ProgID, ClassIcon, ClassIcon);
                progidNodes[i].ToolTipText = BuildProgIDToolTip(ent);
                progidNodes[i].Tag = ent;
                if (ent.Entry != null)
                {
                    progidNodes[i].Nodes.Add("IUnknown");
                }
                i++;
            }
            
            treeComRegistry.Nodes.AddRange(progidNodes);
            TabText = "ProgIDs";
        }

        private void LoadCLSIDsByNames()
        {
            int i = 0;
            TreeNode[] clsidNameNodes = new TreeNode[m_reg.ClsidsByName.Length];
            foreach (COMCLSIDEntry ent in m_reg.ClsidsByName)
            {
                clsidNameNodes[i] = new TreeNode(ent.Name, ClassIcon, ClassIcon);
                clsidNameNodes[i].ToolTipText = BuildCLSIDToolTip(ent);
                clsidNameNodes[i].Tag = ent;
                clsidNameNodes[i].Nodes.Add("IUnknown");
                i++;
            }
            
            treeComRegistry.Nodes.AddRange(clsidNameNodes);

            TabText = "CLSIDs by Name";
        }

        private void LoadCLSIDByServer(bool localServer)
        {            
            int i = 0;
            SortedDictionary<string, List<COMCLSIDEntry>> dict = localServer ? m_reg.ClsidsByLocalServer : m_reg.ClsidsByServer;            
            
            TreeNode[] serverNodes = new TreeNode[dict.Keys.Count];
            foreach (KeyValuePair<string, List<COMCLSIDEntry>> pair in dict)
            {                                
                serverNodes[i] = new TreeNode(pair.Key);
                serverNodes[i].ToolTipText = pair.Key;
            
                TreeNode[] clsidNodes = new TreeNode[pair.Value.Count];
                string[] nodeNames = new string[pair.Value.Count];
                int j = 0;

                foreach(COMCLSIDEntry ent in pair.Value)
                {
                    TreeNode currNode = new TreeNode(ent.Name, ClassIcon, ClassIcon);
                    currNode.ToolTipText = BuildCLSIDToolTip(ent);
                    currNode.Tag = ent;
                    currNode.Nodes.Add("IUnknown");
                    clsidNodes[j] = currNode;
                    nodeNames[j] = ent.Name;
                    j++;
                }

                Array.Sort(nodeNames, clsidNodes);
                serverNodes[i].Nodes.AddRange(clsidNodes);
                
                i++;
            }

            treeComRegistry.Nodes.AddRange(serverNodes);
            TabText = "CLSIDs by Server";
        }

        private void LoadInterfaces()
        {
            int i = 0;
            TreeNode[] iidNodes = new TreeNode[m_reg.Interfaces.Count];
            foreach (COMInterfaceEntry ent in m_reg.Interfaces.Values)
            {
                iidNodes[i] = CreateInterfaceNode(ent);
                i++;
            }
            treeComRegistry.Nodes.AddRange(iidNodes);
            TabText = "Interfaces";
        }

        private void LoadInterfacesByName()
        {                  
            int i = 0;
            TreeNode[] iidNameNodes = new TreeNode[m_reg.InterfacesByName.Length];
            foreach (COMInterfaceEntry ent in m_reg.InterfacesByName)
            {
                iidNameNodes[i] = CreateInterfaceNameNode(ent);                
                i++;
            }
            treeComRegistry.Nodes.AddRange(iidNameNodes);
            TabText = "Interfaces by Name";        
        }

        private void LoadImplementedCategories()
        {
            int i = 0;
            Dictionary<Guid, List<COMCLSIDEntry>> dict = m_reg.ImplementedCategories;
            SortedDictionary<string, TreeNode> sortedNodes = new SortedDictionary<string, TreeNode>();
            
            foreach (KeyValuePair<Guid, List<COMCLSIDEntry>> pair in dict)
            {               
                TreeNode currNode = new TreeNode(COMUtilities.GetCategoryName(pair.Key));
                currNode.Tag = pair.Key;
                currNode.ToolTipText = String.Format("CATID: {0}", pair.Key.ToString("B"));
                sortedNodes.Add(currNode.Text, currNode);

                TreeNode[] clsidNodes = new TreeNode[pair.Value.Count];
                COMCLSIDEntry[] entries = pair.Value.ToArray();
                Array.Sort(entries);
                i = 0;
                foreach (COMCLSIDEntry ent in entries)
                {
                    clsidNodes[i] = new TreeNode(ent.Name, ClassIcon, ClassIcon);
                    clsidNodes[i].ToolTipText = BuildCLSIDToolTip(ent);
                    clsidNodes[i].Tag = ent;
                    clsidNodes[i].Nodes.Add("IUnknown");
                    i++;
                }
                currNode.Nodes.AddRange(clsidNodes);
            }


            TreeNode[] catNodes = new TreeNode[sortedNodes.Count];
            i = 0;
            foreach (KeyValuePair<string, TreeNode> pair in sortedNodes)
            {
                catNodes[i++] = pair.Value;
            }            

            treeComRegistry.Nodes.AddRange(catNodes);
            TabText = "Implemented Categories";            
        }

        private void LoadPreApproved()
        {
            int i = 0;
            TreeNode[] clsidNodes = new TreeNode[m_reg.PreApproved.Length];
            foreach (COMCLSIDEntry ent in m_reg.PreApproved)
            {
                clsidNodes[i] = CreateCLSIDNode(ent);
                i++;
            }
            
            treeComRegistry.Nodes.AddRange(clsidNodes);
            TabText = "Explorer PreApproved";   
        }

        private void LoadIELowRights()
        {
            int i = 0;
            TreeNode[] clsidNodes = new TreeNode[m_reg.LowRights.Length];
            foreach (COMIELowRightsElevationPolicy ent in m_reg.LowRights)
            {
                clsidNodes[i] = new TreeNode(ent.Name);
                clsidNodes[i].ToolTipText = String.Format("Elevation Policy: {0}", ent.Policy); 
                foreach (COMCLSIDEntry cls in ent.Clsids)
                {
                    clsidNodes[i].Nodes.Add(CreateCLSIDNode(cls));
                }
                i++;
            }

            treeComRegistry.Nodes.AddRange(clsidNodes);
            TabText = "IE Low Rights Elevation Policy"; 
        }

        private void SetupCLSIDNodeTree(TreeNode node, bool bRefresh)
        {
            try
            {
                COMCLSIDEntry clsid = null;

                if (node.Tag is COMCLSIDEntry)
                {
                    clsid = (COMCLSIDEntry)node.Tag;

                }
                else if (node.Tag is COMProgIDEntry)
                {
                    clsid = ((COMProgIDEntry)node.Tag).Entry;
                }

                if (clsid != null)
                {
                    node.Nodes.Clear();
                    COMInterfaceEntry[] intEntries = m_reg.GetSupportedInterfaces(clsid, bRefresh);

                    foreach (COMInterfaceEntry ent in intEntries)
                    {
                        node.Nodes.Add(CreateInterfaceNameNode(ent));
                    }
                }
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(String.Format("Error querying COM interfaces\n{0}", ex.Message), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void treeComRegistry_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {            
            Cursor currCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;

            SetupCLSIDNodeTree(e.Node, false);

            Cursor.Current = currCursor;
        }

        enum CopyGuidType
        {
            CopyAsString,
            CopyAsStructure,
            CopyAsObject,
            CopyAsHexString
        }

        private void CopyGuidToClipboard(Guid guid, CopyGuidType copyType)
        {
            string strCopy = null;

            switch (copyType)
            {
                case CopyGuidType.CopyAsObject:
                    strCopy = String.Format("<object id=\"obj\" classid=\"clsid:{0}\">NO OBJECT</object>",
                        guid.ToString());
                    break;
                case CopyGuidType.CopyAsString:
                    strCopy = guid.ToString("B");
                    break;
                case CopyGuidType.CopyAsStructure:
                    {
                        MemoryStream ms = new MemoryStream(guid.ToByteArray());
                        BinaryReader reader = new BinaryReader(ms);
                        strCopy = "struct GUID guidObject = { ";
                        strCopy += String.Format("0x{0:X08}, 0x{1:X04}, 0x{2:X04}, {{", reader.ReadUInt32(),
                            reader.ReadUInt16(), reader.ReadUInt16());
                        for (int i = 0; i < 8; i++)
                        {
                            strCopy += String.Format("0x{0:X02}, ", reader.ReadByte());
                        }
                        strCopy += "}};";
                    }
                    break;
                case CopyGuidType.CopyAsHexString:
                    byte[] data = guid.ToByteArray();
                    strCopy = "";
                    foreach (byte b in data)
                    {
                        strCopy += String.Format("{0:X02}", b);
                    }
                    break;
            }

            if (strCopy != null)
            {
                Clipboard.SetText(strCopy);
            }
        }

        private void copyGUIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeComRegistry.SelectedNode;
            Guid guid = Guid.Empty;

            if (node != null)
            {
                if (node.Tag is COMCLSIDEntry)
                {
                    guid = ((COMCLSIDEntry)node.Tag).Clsid;
                }
                else if (node.Tag is COMInterfaceEntry)
                {
                    guid = ((COMInterfaceEntry)node.Tag).Iid;
                }
                else if (node.Tag is COMProgIDEntry)
                {
                    COMProgIDEntry ent = (COMProgIDEntry)node.Tag;
                    if (ent.Entry != null)
                    {
                        guid = ent.Entry.Clsid;
                    }
                }
                else if (node.Tag is Guid)
                {
                    guid = (Guid)node.Tag;
                }

                if (guid != Guid.Empty)
                {
                    CopyGuidToClipboard(guid, CopyGuidType.CopyAsString);
                }
            }
        }

        private void copyGUIDCStructureToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeComRegistry.SelectedNode;
            Guid guid = Guid.Empty;

            if (node != null)
            {
                if (node.Tag is COMCLSIDEntry)
                {
                    guid = ((COMCLSIDEntry)node.Tag).Clsid;
                }
                else if (node.Tag is COMInterfaceEntry)
                {
                    guid = ((COMInterfaceEntry)node.Tag).Iid;
                }
                else if (node.Tag is COMProgIDEntry)
                {
                    COMProgIDEntry ent = (COMProgIDEntry)node.Tag;
                    if (ent.Entry != null)
                    {
                        guid = ent.Entry.Clsid;
                    }
                }
                else if (node.Tag is Guid)
                {
                    guid = (Guid)node.Tag;
                }

                if (guid != Guid.Empty)
                {
                    CopyGuidToClipboard(guid, CopyGuidType.CopyAsStructure);
                }
            }
        }

        private void copyGUIDHexStringToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeComRegistry.SelectedNode;
            Guid guid = Guid.Empty;

            if (node != null)
            {
                if (node.Tag is COMCLSIDEntry)
                {
                    guid = ((COMCLSIDEntry)node.Tag).Clsid;
                }
                else if (node.Tag is COMInterfaceEntry)
                {
                    guid = ((COMInterfaceEntry)node.Tag).Iid;
                }
                else if (node.Tag is COMProgIDEntry)
                {
                    COMProgIDEntry ent = (COMProgIDEntry)node.Tag;
                    if (ent.Entry != null)
                    {
                        guid = ent.Entry.Clsid;
                    }
                }
                else if (node.Tag is Guid)
                {
                    guid = (Guid)node.Tag;
                }

                if (guid != Guid.Empty)
                {
                    CopyGuidToClipboard(guid, CopyGuidType.CopyAsHexString);
                }
            }
        }

        private void copyObjectTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeComRegistry.SelectedNode;
            Guid guid = Guid.Empty;

            if (node != null)
            {
                if (node.Tag is COMCLSIDEntry)
                {
                    guid = ((COMCLSIDEntry)node.Tag).Clsid;
                }
                else if (node.Tag is COMProgIDEntry)
                {
                    COMProgIDEntry ent = (COMProgIDEntry)node.Tag;
                    if (ent.Entry != null)
                    {
                        guid = ent.Entry.Clsid;
                    }
                }

                if (guid != Guid.Empty)
                {
                    CopyGuidToClipboard(guid, CopyGuidType.CopyAsObject);
                }
            }
        }

        private void createInstanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeComRegistry.SelectedNode;

            if (node != null)
            {
                COMCLSIDEntry ent = null;
                if (node.Tag is COMCLSIDEntry)
                {
                    ent = (COMCLSIDEntry)node.Tag;
                }
                else if (node.Tag is COMProgIDEntry)
                {
                    ent = ((COMProgIDEntry)node.Tag).Entry;
                }
                
                if(ent != null)
                {                    
                    Dictionary<string, string> props = new Dictionary<string,string>();
                    try
                    {
                        object comObj = ent.CreateInstanceAsObject();
                        if (comObj != null)
                        {                            
                            props.Add("CLSID", ent.Clsid.ToString("B"));
                            props.Add("Name", ent.Name);
                            props.Add("Server", ent.Server);
                            
                            /* Need to implement a type library reader */
                            Type dispType = COMUtilities.GetDispatchTypeInfo(comObj);

                            ObjectInformation view = new ObjectInformation(ent.Name, comObj, props, m_reg.GetSupportedInterfaces(ent, false));
                            view.ShowHint = DockState.Document;
                            view.Show(this.DockPanel);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }    

        private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            TreeNode node = treeComRegistry.SelectedNode;

            if ((node != null) && (node.Tag != null))
            {
                contextMenuStrip.Items.Clear();
                contextMenuStrip.Items.Add(copyGUIDToolStripMenuItem);
                contextMenuStrip.Items.Add(copyGUIDHexStringToolStripMenuItem);
                contextMenuStrip.Items.Add(copyGUIDCStructureToolStripMenuItem);
                if ((node.Tag is COMCLSIDEntry) || (node.Tag is COMProgIDEntry))
                {
                    contextMenuStrip.Items.Add(copyObjectTagToolStripMenuItem);
                    contextMenuStrip.Items.Add(createInstanceToolStripMenuItem);
                    contextMenuStrip.Items.Add(refreshInterfacesToolStripMenuItem);
                }
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void refreshInterfacesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode node = treeComRegistry.SelectedNode;
            if ((node != null) && (node.Tag != null))
            {
                SetupCLSIDNodeTree(node, true);
            }
        }

        /// <summary>
        /// Convert a basic Glob to a regular expression
        /// </summary>
        /// <param name="glob">The glob string</param>
        /// <param name="ignoreCase">Indicates that match should ignore case</param>
        /// <returns>The regular expression</returns>
        private static Regex GlobToRegex(string glob, bool ignoreCase)
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("^");

            foreach (char ch in glob)
            {
                if (ch == '*')
                {
                    builder.Append(".*");
                }
                else if (ch == '?')
                {
                    builder.Append(".");
                }
                else
                {
                    builder.Append(Regex.Escape(new String(ch, 1)));
                }
            }

            builder.Append("$");

            return new Regex(builder.ToString(), ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }

        private static Func<TreeNode, bool> CreateFilter(string filter, int mode, bool caseSensitive)
        {                        
            StringComparison comp;

            if(caseSensitive)
            {
                comp = StringComparison.CurrentCulture;
            }
            else
            {
                comp = StringComparison.CurrentCultureIgnoreCase;
            }

            switch (mode)
            {
                case 0:
                    if (caseSensitive)
                    {
                        return n => n.Text.Contains(filter);
                    }
                    else
                    {
                        filter = filter.ToLower();
                        return n => n.Text.ToLower().Contains(filter.ToLower());
                    }
                case 1:
                    return n => n.Text.StartsWith(filter, comp);
                case 2:
                    return n => n.Text.EndsWith(filter, comp);
                case 3:
                    return n => n.Text.Equals(filter, comp);
                case 4:
                    {
                        Regex r = GlobToRegex(filter, caseSensitive);

                        return n => r.IsMatch(n.Text);
                    }
                case 5:
                    {
                        Regex r = new Regex(filter, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

                        return n => r.IsMatch(n.Text);
                    }
                default:
                    throw new ArgumentException("Invalid mode value");
            }

        }

        // Check if top node or one of its subnodes matches the filter
        private static bool FilterNode(TreeNode n, Func<TreeNode, bool> filterFunc)
        {
            bool result = filterFunc(n);

            if (!result)
            {
                foreach (TreeNode node in n.Nodes)
                {
                    result = filterFunc(node);
                    if (result)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            try
            {                
                string filter = textBoxFilter.Text.Trim();
                TreeNode[] nodes;

                if (filter.Length > 0)
                {
                    Func<TreeNode, bool> filterFunc = CreateFilter(filter, comboBoxMode.SelectedIndex, false);
                    

                    nodes = m_originalNodes.Where(n => FilterNode(n, filterFunc)).ToArray();
                }
                else
                {
                    nodes = m_originalNodes;
                }

                treeComRegistry.SuspendLayout();
                treeComRegistry.Nodes.Clear();
                treeComRegistry.Nodes.AddRange(nodes);
                treeComRegistry.ResumeLayout();
            }
            catch(ArgumentException ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBoxFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Enter) || (e.KeyCode == Keys.Return))
            {
                btnApply.PerformClick();
            }
        }

    }
}
