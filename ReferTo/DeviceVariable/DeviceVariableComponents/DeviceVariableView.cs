using CxWinFormsComponents;
using DeviceInterfaces;
using DeviceInterfaces.UIStructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeviceVariableComponents
{
    public class DeviceVariableView
    {
        private DataGridView deviceVariablesDataGridView;

        List<DeviceVariableDetail> deviceVariableDetailList;
        SortOrder sortOrder = SortOrder.None;
        string sortColumnName = string.Empty;

        public DeviceVariableView(DataGridView deviceVariablesDataGridView)
        {
            this.deviceVariablesDataGridView = deviceVariablesDataGridView;

            Init();
        }

        public void LoadDevice(string deviceName)
        {
            deviceVariableDetailList = DeviceManager.ExportDeviceVariableDetail(deviceName, true);
            SortDataGridView();
            LoadDeviceProperties();
        }

        private void Init()
        {
            // 动态创建列
            deviceVariablesDataGridView.AutoGenerateColumns = false;
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "DeviceName", DataPropertyName = "DeviceName", HeaderText = "设备名称", Width = 150 });
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "VariableName", DataPropertyName = "DeviceName", HeaderText = "属性名称", Width = 150 });
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "VariableTitle", DataPropertyName = "VariableTitle", HeaderText = "属性标题", Width = 150 });
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", DataPropertyName = "Value", HeaderText = "属性值", Width = 100 });
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastUpdatedString", DataPropertyName = "LastUpdatedString", HeaderText = "最后更新时间", Width = 150 });
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "ValueTypeName", DataPropertyName = "ValueTypeName", HeaderText = "值类型名", Width = 100 });
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "ValueEnumTypeName", DataPropertyName = "ValueEnumTypeName", HeaderText = "值枚举类型名", Width = 150 });
            //dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "HasInitialValue", DataPropertyName = "HasInitialValue", HeaderText = "是否要初始值", Width = 100 });
            deviceVariablesDataGridView.Columns.Add("DeviceName", "设备名称");
            deviceVariablesDataGridView.Columns.Add("VariableName", "属性名称");
            deviceVariablesDataGridView.Columns.Add("VariableTitle", "属性标题");
            deviceVariablesDataGridView.Columns.Add("Value", "属性值");
            deviceVariablesDataGridView.Columns.Add("LastUpdatedString", "更新时间");
            deviceVariablesDataGridView.Columns.Add("ValueTypeName", "值类型");
            deviceVariablesDataGridView.Columns.Add("ValueEnumTypeName", "值枚举类");
            deviceVariablesDataGridView.Columns.Add("HasInitialValue", "要初始值");

            this.deviceVariablesDataGridView.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView_CellEndEdit);
            this.deviceVariablesDataGridView.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.dataGridView_ColumnHeaderMouseClick);
            this.deviceVariablesDataGridView.RowStateChanged += new System.Windows.Forms.DataGridViewRowStateChangedEventHandler(this.dataGridView_RowStateChanged);

            AdjustDataGridViewColumnWidths(deviceVariablesDataGridView, new Dictionary<string, int>
            {
                {"DeviceName", 100},
                {"VariableName", 100},
                {"VariableTitle", 100},
                {"Value", 80},
                {"LastUpdatedString", 180},
                {"ValueTypeName", 80},
                {"ValueEnumTypeName", 100},
                {"HasInitialValue", 100},
            });
        }

        private void LoadDeviceProperties()
        {
            // 获取所有设备的动态属性详情
            //dataGridView.DataSource = null;
            //dataGridView.DataSource = dvList;

            // 如果设备列表为空，则清空DataGridView并返回
            if (deviceVariableDetailList == null || deviceVariableDetailList.Count == 0)
            {
                deviceVariablesDataGridView.Rows.Clear();
                return;
            }

            // 如果行数与设备变量详情列表计数不一致，则重新加载整个列表
            if (deviceVariablesDataGridView.Rows.Count != deviceVariableDetailList.Count)
            {
                deviceVariablesDataGridView.Rows.Clear();
                foreach (var detail in deviceVariableDetailList)
                {
                    deviceVariablesDataGridView.Rows.Add(detail.DeviceName, detail.VariableName, detail.VariableTitle, detail.Value, detail.LastUpdated, detail.ValueTypeName, detail.ValueEnumTypeName, detail.HasInitialValue);
                }
            }
            else
            {
                // 如果行数一致，更新现有行的值
                for (int i = 0; i < deviceVariableDetailList.Count; i++)
                {
                    var row = deviceVariablesDataGridView.Rows[i];
                    var detail = deviceVariableDetailList[i];
                    row.SetValues(detail.DeviceName, detail.VariableName, detail.VariableTitle, detail.Value, detail.LastUpdated, detail.ValueTypeName, detail.ValueEnumTypeName, detail.HasInitialValue);
                }
            }

            DataGridViewHelper.BeautifyDataGridView1(deviceVariablesDataGridView);
        }

        private void dataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // 清除错误消息
            deviceVariablesDataGridView.Rows[e.RowIndex].ErrorText = null;
        }

        private void dataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            DataGridViewColumn column = deviceVariablesDataGridView.Columns[e.ColumnIndex];
            SortDataGridView(column);
        }

        private void dataGridView_RowStateChanged(object sender, DataGridViewRowStateChangedEventArgs e)
        {
            e.Row.HeaderCell.Value = string.Format("{0}", e.Row.Index + 1);
        }

        private void SortDataGridView(DataGridViewColumn column)
        {
            sortOrder = column.HeaderCell.SortGlyphDirection == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            sortColumnName = column.Name;
            SortDataGridView();
            // 更新数据源
            //dataGridView.DataSource = null;
            //dataGridView.DataSource = data;
            LoadDeviceProperties();
            // 更新排序图标
            column.HeaderCell.SortGlyphDirection = sortOrder;
        }

        private void SortDataGridView()
        {
            // 根据列名选择排序逻辑
            switch (sortColumnName)
            {
                case "DeviceName":
                    deviceVariableDetailList = sortOrder == SortOrder.Ascending ? deviceVariableDetailList.OrderBy(p => p.DeviceName).ToList() : deviceVariableDetailList.OrderByDescending(p => p.DeviceName).ToList();
                    break;
                case "VariableName":
                    deviceVariableDetailList = sortOrder == SortOrder.Ascending ? deviceVariableDetailList.OrderBy(p => p.VariableName).ToList() : deviceVariableDetailList.OrderByDescending(p => p.VariableName).ToList();
                    break;
                case "VariableTitle":
                    deviceVariableDetailList = sortOrder == SortOrder.Ascending ? deviceVariableDetailList.OrderBy(p => p.VariableTitle).ToList() : deviceVariableDetailList.OrderByDescending(p => p.VariableTitle).ToList();
                    break;
                case "Value":
                    deviceVariableDetailList = sortOrder == SortOrder.Ascending ? deviceVariableDetailList.OrderBy(p => p.Value).ToList() : deviceVariableDetailList.OrderByDescending(p => p.Value).ToList();
                    break;
                case "LastUpdatedString":
                    deviceVariableDetailList = sortOrder == SortOrder.Ascending ? deviceVariableDetailList.OrderBy(p => p.LastUpdatedString).ToList() : deviceVariableDetailList.OrderByDescending(p => p.LastUpdatedString).ToList();
                    break;
                case "ValueEnumTypeName":
                    deviceVariableDetailList = sortOrder == SortOrder.Ascending ? deviceVariableDetailList.OrderBy(p => p.ValueEnumTypeName).ToList() : deviceVariableDetailList.OrderByDescending(p => p.ValueEnumTypeName).ToList();
                    break;
                case "HasInitialValue":
                    deviceVariableDetailList = sortOrder == SortOrder.Ascending ? deviceVariableDetailList.OrderBy(p => p.HasInitialValue).ToList() : deviceVariableDetailList.OrderByDescending(p => p.HasInitialValue).ToList();
                    break;
            }
        }

        private void AdjustDataGridViewColumnWidths(DataGridView dataGridView, Dictionary<string, int> columnWidths)
        {
            foreach (var col in dataGridView.Columns.Cast<DataGridViewColumn>())
            {
                if (columnWidths.TryGetValue(col.Name, out int width))
                {
                    col.Width = width;
                }
            }
        }
    }

}
