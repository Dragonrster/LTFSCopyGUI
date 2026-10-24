Imports System.Collections
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Windows.Forms

Public Class CmViewForm
    Inherits Form

    Private ReadOnly _openButton As New Button()
    Private ReadOnly _fullDataButton As New Button()
    Private ReadOnly _rawButton As New Button()
    Private ReadOnly _batchButton As New Button()
    Private ReadOnly _fileLabel As New Label()
    Private ReadOnly _sectionsPanel As New TableLayoutPanel()
    Private ReadOnly _statusStrip As New StatusStrip()
    Private ReadOnly _statusLabel As New ToolStripStatusLabel()
    Private ReadOnly _toolTip As New ToolTip()
    Private _boldFont As Font
    Private _currentReport As CmReport
    Private _initialFile As String

    Public Sub New()
        Text = "CM 解析器"
        StartPosition = FormStartPosition.CenterScreen
        Size = New Size(920, 740)
        MinimumSize = New Size(760, 520)
        Font = New Font("Microsoft YaHei UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.FromArgb(246, 248, 251)
        AllowDrop = True
        KeyPreview = True
        _boldFont = New Font(Font, FontStyle.Bold)

        Dim topPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .Height = 52,
            .ColumnCount = 5,
            .RowCount = 1,
            .Padding = New Padding(8, 10, 8, 8),
            .BackColor = Color.White
        }
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 112.0F))
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 92.0F))
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 92.0F))
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 92.0F))
        topPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        _openButton.Text = "打开 CM 文件"
        _openButton.Dock = DockStyle.Fill
        _openButton.Margin = New Padding(0, 0, 8, 0)
        AddHandler _openButton.Click, AddressOf OpenFileClicked

        _fileLabel.AutoEllipsis = True
        _fileLabel.Dock = DockStyle.Fill
        _fileLabel.TextAlign = ContentAlignment.MiddleLeft
        _fileLabel.ForeColor = Color.DimGray
        _fileLabel.Margin = New Padding(0)
        _fileLabel.Text = "尚未加载 CM 文件（可将 .cm 文件拖放到窗口中）"

        _fullDataButton.Text = "完整解析"
        _fullDataButton.Dock = DockStyle.Fill
        _fullDataButton.Margin = New Padding(8, 0, 0, 0)
        _fullDataButton.Enabled = False
        AddHandler _fullDataButton.Click, AddressOf ShowFullDataClicked

        _rawButton.Text = "原始报告"
        _rawButton.Dock = DockStyle.Fill
        _rawButton.Margin = New Padding(8, 0, 0, 0)
        _rawButton.Enabled = False
        AddHandler _rawButton.Click, AddressOf ShowRawReportClicked

        _batchButton.Text = "批量管理"
        _batchButton.Dock = DockStyle.Fill
        _batchButton.Margin = New Padding(8, 0, 0, 0)
        _toolTip.SetToolTip(_batchButton, "递归扫描文件夹中的 CM 文件并列表总览")
        AddHandler _batchButton.Click, AddressOf ShowBatchClicked

        topPanel.Controls.Add(_openButton, 0, 0)
        topPanel.Controls.Add(_fileLabel, 1, 0)
        topPanel.Controls.Add(_fullDataButton, 2, 0)
        topPanel.Controls.Add(_rawButton, 3, 0)
        topPanel.Controls.Add(_batchButton, 4, 0)

        _sectionsPanel.Dock = DockStyle.Fill
        _sectionsPanel.ColumnCount = 1
        _sectionsPanel.RowCount = 0
        _sectionsPanel.AutoScroll = True
        _sectionsPanel.Padding = New Padding(10, 10, 10, 10)
        _sectionsPanel.BackColor = Color.FromArgb(246, 248, 251)
        _sectionsPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        _statusStrip.Items.Add(_statusLabel)
        _statusLabel.Text = "就绪"
        _statusLabel.Spring = True
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft
        _statusStrip.BackColor = Color.White

        Controls.Add(_sectionsPanel)
        Controls.Add(topPanel)
        Controls.Add(_statusStrip)
    End Sub

    Public Property InitialFile As String
        Get
            Return _initialFile
        End Get
        Set(value As String)
            _initialFile = value
        End Set
    End Property

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        If Not String.IsNullOrWhiteSpace(_initialFile) Then LoadFile(_initialFile)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.Control AndAlso e.KeyCode = Keys.O Then
            OpenFileClicked(Me, EventArgs.Empty)
            e.Handled = True
        End If
        MyBase.OnKeyDown(e)
    End Sub

    Protected Overrides Sub OnDragEnter(e As DragEventArgs)
        If e.Data IsNot Nothing AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        End If
        MyBase.OnDragEnter(e)
    End Sub

    Protected Overrides Sub OnDragDrop(e As DragEventArgs)
        If e.Data IsNot Nothing AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files As String() = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files IsNot Nothing AndAlso files.Length > 0 Then LoadFile(files(0))
        End If
        MyBase.OnDragDrop(e)
    End Sub

    Private Sub OpenFileClicked(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog()
            dialog.Title = "打开 CM 文件"
            dialog.Filter = "CM 文件 (*.cm;*.txt;*.bin)|*.cm;*.txt;*.bin|所有文件 (*.*)|*.*"
            dialog.CheckFileExists = True
            If dialog.ShowDialog(Me) = DialogResult.OK Then LoadFile(dialog.FileName)
        End Using
    End Sub

    Private Sub ShowRawReportClicked(sender As Object, e As EventArgs)
        If _currentReport Is Nothing Then Return
        Using dialog As New RawReportForm(_currentReport.SourcePath, _currentReport.ReportText)
            dialog.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowFullDataClicked(sender As Object, e As EventArgs)
        If _currentReport Is Nothing Then Return
        If Not _currentReport.HasFullData Then
            MessageBox.Show(Me, "该文件不包含 CM 原始数据（CM RAW DATA），无法查看完整解析结果。", "完整解析", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Using dialog As New FullDataForm(_currentReport)
            dialog.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowBatchClicked(sender As Object, e As EventArgs)
        Using dialog As New CmBatchForm()
            If _currentReport IsNot Nothing Then
                Dim folder As String = ""
                Try
                    folder = IO.Path.GetDirectoryName(_currentReport.SourcePath)
                Catch
                End Try
                If Not String.IsNullOrEmpty(folder) Then dialog.InitialFolder = folder
            End If
            dialog.ShowDialog(Me)
        End Using
    End Sub

    Public Sub LoadFile(path As String)
        Try
            Dim report As CmReport = CmReport.ParseFile(path)
            _currentReport = report
            _rawButton.Enabled = True
            _fileLabel.Text = IO.Path.GetFileName(path)
            _toolTip.SetToolTip(_fileLabel, path)

            Dim title As String = If(String.IsNullOrWhiteSpace(report.Barcode), "CM 解析器", report.Barcode & " - CM 解析器")
            Text = title
            BuildUi(report)

            _fullDataButton.Enabled = report.HasFullData
            If report.HasFullData Then
                _toolTip.SetToolTip(_fullDataButton, "查看从 CM 原始 8160 字节数据解析出的全部字段")
            Else
                _toolTip.SetToolTip(_fullDataButton, "该文件不包含 CM 原始数据（CM RAW DATA），只能显示报告字段")
            End If

            Dim sizeText As String = ""
            Try
                sizeText = $"    文件大小 {New FileInfo(path).Length:N0} 字节"
            Catch
            End Try
            If report.HasFullData Then
                _statusLabel.Text = $"已加载：{IO.Path.GetFileName(path)}{sizeText}（含完整 CM 数据）"
            Else
                _statusLabel.Text = $"已加载：{IO.Path.GetFileName(path)}{sizeText}（无原始 CM 数据，仅报告字段）"
            End If
            _statusLabel.ForeColor = Color.FromArgb(0, 100, 0)
        Catch ex As FormatException
            ShowLoadError(ex.Message)
        Catch ex As Exception
            ShowLoadError("解析失败：" & ex.Message)
        End Try
    End Sub

    Private Sub ShowLoadError(message As String)
        _statusLabel.Text = "加载失败：" & message
        _statusLabel.ForeColor = Color.Firebrick
        MessageBox.Show(Me, message, "打开 CM 文件失败", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

    Private Sub BuildUi(report As CmReport)
        _sectionsPanel.SuspendLayout()
        Try
            _sectionsPanel.Controls.Clear()
            _sectionsPanel.RowStyles.Clear()
            _sectionsPanel.RowCount = 0

            AddSection(BuildApplicationSection(report))
            AddSection(BuildUsageSection(report))
            AddSection(BuildIdentitySection(report))
            AddSection(BuildDataSection(report))
        Finally
            _sectionsPanel.ResumeLayout(True)
        End Try
    End Sub

    Private Sub AddSection(section As TableLayoutPanel)
        Dim row As Integer = _sectionsPanel.RowCount
        _sectionsPanel.RowCount = row + 1
        _sectionsPanel.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        _sectionsPanel.Controls.Add(section, 0, row)
    End Sub

    Private Function CreateSection(title As String) As TableLayoutPanel
        Dim table As New TableLayoutPanel() With {
            .ColumnCount = 4,
            .RowCount = 1,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Dock = DockStyle.Fill,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 12)
        }
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 20.0F))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 20.0F))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        table.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))

        Dim header As New Label() With {
            .Text = title,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font(_boldFont, FontStyle.Bold),
            .BackColor = Color.FromArgb(221, 230, 241),
            .ForeColor = Color.FromArgb(32, 48, 72),
            .Padding = New Padding(8, 0, 0, 0),
            .Margin = New Padding(0)
        }
        table.Controls.Add(header, 0, 0)
        table.SetColumnSpan(header, 4)
        Return table
    End Function

    Private Sub AddInfoRow(table As TableLayoutPanel, label1 As String, value1 As String, label2 As String, value2 As String)
        Dim row As Integer = table.RowCount
        table.RowCount = row + 1
        table.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))

        table.Controls.Add(CreateCellLabel(label1, True), 0, row)
        table.Controls.Add(CreateCellLabel(value1, False), 1, row)
        table.Controls.Add(CreateCellLabel(If(String.IsNullOrEmpty(label2), "", label2), True), 2, row)
        table.Controls.Add(CreateCellLabel(If(String.IsNullOrEmpty(label2), "", value2), False), 3, row)
    End Sub

    Private Function CreateCellLabel(text As String, isLabel As Boolean) As Label
        Dim label As New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoSize = False,
            .AutoEllipsis = isLabel,
            .Margin = New Padding(0),
            .Padding = New Padding(8, 0, 4, 0),
            .ForeColor = If(isLabel, Color.FromArgb(96, 96, 96), Color.Black),
            .Font = If(isLabel, Font, _boldFont)
        }
        Return label
    End Function

    Private Function BuildApplicationSection(report As CmReport) As TableLayoutPanel
        Dim table As TableLayoutPanel = CreateSection("应用信息")
        AddInfoRow(table, "条码", CmReport.ToDisplayValue(report.Barcode), "应用", CmReport.ToDisplayValue(report.Application))
        Return table
    End Function

    Private Function BuildUsageSection(report As CmReport) As TableLayoutPanel
        Dim table As TableLayoutPanel = CreateSection("使用情况")
        If report.IsCleanCartridge Then
            AddInfoRow(table, "清洗次数", CmReport.ToDisplayValue(report.CleansPerformed),
                           "剩余清洗次数", CmReport.ToDisplayValue(report.CleansRemain))
            AddInfoRow(table, "已用长度", CmReport.ToDisplayValue(report.UsedLength), "", "")
        Else
            AddInfoRow(table, "加载次数", CmReport.ToDisplayValue(report.LoadCount),
                           "累计写入", CmReport.ToDisplayValue(report.TotalWrite))
            AddInfoRow(table, "累计读取", CmReport.ToDisplayValue(report.TotalRead),
                           "完整卷等效量", CmReport.ToDisplayValue(report.FullVolumeEquivalents))
            AddInfoRow(table, "写入重试次数", CmReport.ToDisplayValue(report.WriteRetries),
                           "读取重试次数", CmReport.ToDisplayValue(report.ReadRetries))
            AddInfoRow(table, "不可恢复写入错误", CmReport.ToDisplayValue(report.UnrecoveredWrites),
                           "不可恢复读取错误", CmReport.ToDisplayValue(report.UnrecoveredReads))
            AddInfoRow(table, "暂停写入", CmReport.ToDisplayValue(report.SuspendedWrites),
                           "暂停追加写入", CmReport.ToDisplayValue(report.SuspendedAppendWrites))
            AddInfoRow(table, "致命暂停写入", CmReport.ToDisplayValue(report.FatalSuspendedWrites), "", "")
        End If
        Return table
    End Function

    Private Function BuildIdentitySection(report As CmReport) As TableLayoutPanel
        Dim table As TableLayoutPanel = CreateSection("磁带信息")
        AddInfoRow(table, "磁带格式", CmReport.ToDisplayValue(report.MediaFormat),
                       "序列号", CmReport.ToDisplayValue(report.SerialNumber))
        AddInfoRow(table, "磁带厂商", CmReport.ToDisplayValue(report.TapeVendor),
                       "磁带生产日期", CmReport.ToDisplayValue(report.TapeMfgDate))
        AddInfoRow(table, "介质厂商", CmReport.ToDisplayValue(report.MediaVendor),
                       "介质生产日期", CmReport.ToDisplayValue(report.MediaMfgDate))
        AddInfoRow(table, "颗粒类型（仅供参考）", CmReport.ToDisplayValue(report.ParticleType), "", "")
        Return table
    End Function

    Private Function BuildDataSection(report As CmReport) As TableLayoutPanel
        Dim table As TableLayoutPanel = CreateSection("磁带数据")
        AddInfoRow(table, "分区数", CmReport.ToDisplayValue(report.PartitionCount),
                       "估算容量损失", CmReport.ToDisplayValue(report.EstimatedCapacityLoss))

        Dim index As Integer = 0
        While index < report.Partitions.Count
            Dim firstLabel As String = "分区 " & index
            Dim firstValue As String = CmReport.ToDisplayValue(report.Partitions(index))
            Dim secondLabel As String = ""
            Dim secondValue As String = ""
            If index + 1 < report.Partitions.Count Then
                secondLabel = "分区 " & (index + 1)
                secondValue = CmReport.ToDisplayValue(report.Partitions(index + 1))
            End If
            AddInfoRow(table, firstLabel, firstValue, secondLabel, secondValue)
            index += 2
        End While
        Return table
    End Function

End Class

''' <summary>显示 CM 报告原文的只读窗口。</summary>
Friend Class RawReportForm
    Inherits Form

    Public Sub New(path As String, reportText As String)
        Text = If(String.IsNullOrWhiteSpace(path), "原始报告", "原始报告 - " & IO.Path.GetFileName(path))
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(860, 640)
        MinimumSize = New Size(520, 360)
        ShowInTaskbar = False
        MinimizeBox = False

        Dim textBox As New TextBox() With {
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Both,
            .WordWrap = False,
            .Dock = DockStyle.Fill,
            .Font = New Font("Consolas", 9.0F, FontStyle.Regular, GraphicsUnit.Point),
            .BackColor = Color.White,
            .Text = reportText
        }
        Controls.Add(textBox)
    End Sub
End Class

''' <summary>完整 CM 数据浏览窗口：递归列出从原始 CM 数据解析出的全部字段。</summary>
Friend Class FullDataForm
    Inherits Form

    Private ReadOnly _grid As New DataGridView()

    Public Sub New(report As CmReport)
        Text = If(String.IsNullOrWhiteSpace(report.Barcode), "完整 CM 数据", report.Barcode & " - 完整 CM 数据")
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1080, 720)
        MinimumSize = New Size(720, 420)
        ShowInTaskbar = False
        MinimizeBox = False
        BackColor = Color.White

        _grid.Dock = DockStyle.Fill
        _grid.ReadOnly = True
        _grid.AllowUserToAddRows = False
        _grid.AllowUserToDeleteRows = False
        _grid.AllowUserToResizeRows = False
        _grid.RowHeadersVisible = False
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect
        _grid.MultiSelect = True
        _grid.BackgroundColor = Color.White
        _grid.BorderStyle = BorderStyle.None
        _grid.ColumnHeadersDefaultCellStyle.Font = New Font(Font, FontStyle.Bold)
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False
        _grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText

        _grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "分类", .FillWeight = 22})
        _grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "字段", .FillWeight = 30})
        _grid.Columns.Add(New DataGridViewTextBoxColumn() With {.HeaderText = "值", .FillWeight = 48})

        _grid.Rows.Add("CM", "校验结果", If(report.HasFullData, "有效", "无效"))
        If report.ParsedCM IsNot Nothing AndAlso report.ParsedCM.a_CMBuffer IsNot Nothing Then
            _grid.Rows.Add("CM", "原始数据大小", report.ParsedCM.a_CMBuffer.Length & " 字节")
        End If

        Dim rows As New List(Of KeyValuePair(Of String, String))()
        If report.ParsedCM IsNot Nothing Then CollectFromObject(report.ParsedCM, "", rows, 0)

        ' 有意义的分类排前面，内部字段（a_/g_）排最后，同类保持解析顺序
        Dim groupOrder As New List(Of String) From {
            "CartridgeMfgData", "MediaMfgData", "UsageData", "StatusData", "InitialisationData",
            "EndOfDataData", "CartridgeContentData", "TapeDirectoryData", "SuspendWriteData",
            "ApplicationSpecificData", "GeneralData", "PageData"
        }
        Dim ordered As List(Of KeyValuePair(Of String, String)) =
            rows.OrderBy(Function(kv) GetGroupPriority(kv.Key, groupOrder)).ToList()

        For Each pair As KeyValuePair(Of String, String) In ordered
            Dim path As String = pair.Key
            Dim category As String = ""
            Dim field As String = path
            Dim dot As Integer = path.IndexOf("."c)
            If dot >= 0 Then
                category = path.Substring(0, dot)
                field = path.Substring(dot + 1)
            End If
            _grid.Rows.Add(category, field, pair.Value)
        Next

        Controls.Add(_grid)
    End Sub

    Private Shared Function GetGroupPriority(path As String, order As List(Of String)) As Integer
        Dim head As String = path
        Dim dot As Integer = head.IndexOf("."c)
        If dot >= 0 Then head = head.Substring(0, dot)
        Dim bracket As Integer = head.IndexOf("["c)
        If bracket >= 0 Then head = head.Substring(0, bracket)
        Dim index As Integer = order.IndexOf(head)
        If index >= 0 Then Return index
        If head.StartsWith("a_", StringComparison.Ordinal) OrElse head.StartsWith("g_", StringComparison.Ordinal) Then Return 100
        Return 50
    End Function

    Private Shared Sub CollectFromObject(obj As Object, path As String, rows As List(Of KeyValuePair(Of String, String)), depth As Integer)
        If obj Is Nothing Then Return
        If depth > 6 OrElse rows.Count > 5000 Then Return
        Dim t As Type = obj.GetType()

        If IsScalarType(t) Then
            rows.Add(New KeyValuePair(Of String, String)(path, FormatScalar(obj)))
            Return
        End If

        If t.IsArray Then
            Dim arr As Array = DirectCast(obj, Array)
            If t.GetElementType() Is GetType(Byte) Then
                rows.Add(New KeyValuePair(Of String, String)(path, $"byte[{arr.Length}]"))
            Else
                For i As Integer = 0 To arr.Length - 1
                    CollectFromObject(arr.GetValue(i), $"{path}[{i}]", rows, depth + 1)
                Next
            End If
            Return
        End If

        Dim enumerable As IEnumerable = TryCast(obj, IEnumerable)
        If enumerable IsNot Nothing Then
            Dim index As Integer = 0
            For Each item As Object In enumerable
                CollectFromObject(item, $"{path}[{index}]", rows, depth + 1)
                index += 1
            Next
            Return
        End If

        For Each p As PropertyInfo In t.GetProperties(BindingFlags.Public Or BindingFlags.Instance)
            If p.GetIndexParameters().Length > 0 Then Continue For
            Dim childPath As String = If(path.Length = 0, p.Name, path & "." & p.Name)
            Dim value As Object = Nothing
            Try
                value = p.GetValue(obj, Nothing)
            Catch
                Continue For
            End Try
            CollectFromObject(value, childPath, rows, depth + 1)
        Next
        For Each f As FieldInfo In t.GetFields(BindingFlags.Public Or BindingFlags.Instance)
            Dim childPath As String = If(path.Length = 0, f.Name, path & "." & f.Name)
            Dim value As Object = Nothing
            Try
                value = f.GetValue(obj)
            Catch
                Continue For
            End Try
            CollectFromObject(value, childPath, rows, depth + 1)
        Next
    End Sub

    Private Shared Function IsScalarType(t As Type) As Boolean
        Return t.IsPrimitive OrElse t.IsEnum OrElse t Is GetType(String) OrElse t Is GetType(Decimal) OrElse
               t Is GetType(DateTime) OrElse t Is GetType(TimeSpan) OrElse t Is GetType(Guid)
    End Function

    Private Shared Function FormatScalar(obj As Object) As String
        If obj Is Nothing Then Return ""
        Dim s As String
        If TypeOf obj Is DateTime Then
            s = DirectCast(obj, DateTime).ToString("yyyy-MM-dd HH:mm:ss")
        Else
            s = Convert.ToString(obj, Globalization.CultureInfo.InvariantCulture)
        End If
        If s.Length > 400 Then s = s.Substring(0, 400) & "…"
        Return s.Replace(vbCr, " ").Replace(vbLf, " ")
    End Function
End Class
