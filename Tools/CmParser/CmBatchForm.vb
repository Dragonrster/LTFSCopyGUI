Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

''' <summary>
''' 批量 CM 管理：递归扫描选定文件夹中的 CM 文件，表格总览条码、格式、使用情况等字段，并支持关键字筛选。
''' 只解析文件，不访问磁带设备。
''' </summary>
Friend Class CmBatchForm
    Inherits Form

    Private Shared ReadOnly SupportedExtensions As String() = {".cm", ".txt", ".bin"}

    Private ReadOnly _chooseButton As New Button()
    Private ReadOnly _cancelButton As New Button()
    Private ReadOnly _folderLabel As New Label()
    Private ReadOnly _filterBox As New TextBox()
    Private ReadOnly _grid As New DataGridView()
    Private ReadOnly _statusStrip As New StatusStrip()
    Private ReadOnly _statusLabel As New ToolStripStatusLabel()
    Private ReadOnly _progressBar As New ToolStripProgressBar()
    Private ReadOnly _toolTip As New ToolTip()
    Private ReadOnly _boldFont As Font
    Private _scanCancellation As CancellationTokenSource
    Private _isScanning As Boolean
    Private _scanVersion As Integer
    Private _scanFolder As String = ""
    Private _initialFolder As String = ""
    Private _successCount As Integer
    Private _failCount As Integer
    Private _totalCount As Integer
    Private ReadOnly _entries As New List(Of CmBatchEntry)()
    Private _sortColumnIndex As Integer = -1
    Private _sortAscending As Boolean = True

    Public Sub New()
        Text = "批量 CM 管理"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1280, 760)
        MinimumSize = New Size(900, 520)
        Font = New Font("Microsoft YaHei UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.FromArgb(246, 248, 251)
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
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 76.0F))
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 52.0F))
        topPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 260.0F))
        topPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        _chooseButton.Text = "选择文件夹"
        _chooseButton.Dock = DockStyle.Fill
        _chooseButton.Margin = New Padding(0, 0, 8, 0)
        AddHandler _chooseButton.Click, AddressOf ChooseFolderClicked

        _cancelButton.Text = "取消"
        _cancelButton.Dock = DockStyle.Fill
        _cancelButton.Margin = New Padding(0, 0, 8, 0)
        _cancelButton.Enabled = False
        AddHandler _cancelButton.Click, AddressOf CancelClicked

        _folderLabel.AutoEllipsis = True
        _folderLabel.Dock = DockStyle.Fill
        _folderLabel.TextAlign = ContentAlignment.MiddleLeft
        _folderLabel.ForeColor = Color.DimGray
        _folderLabel.Margin = New Padding(0)
        _folderLabel.Text = "尚未选择文件夹（递归扫描其中的 .cm / .txt / .bin 文件）"

        Dim filterLabel As New Label() With {
            .Text = "筛选：",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleRight,
            .Margin = New Padding(0)
        }

        _filterBox.Dock = DockStyle.Fill
        _filterBox.Margin = New Padding(4, 6, 0, 6)
        _filterBox.Enabled = False
        _toolTip.SetToolTip(_filterBox, "输入条码 / 格式 / 序列号 / 文件路径关键字")
        AddHandler _filterBox.TextChanged, AddressOf FilterChanged

        topPanel.Controls.Add(_chooseButton, 0, 0)
        topPanel.Controls.Add(_cancelButton, 1, 0)
        topPanel.Controls.Add(_folderLabel, 2, 0)
        topPanel.Controls.Add(filterLabel, 3, 0)
        topPanel.Controls.Add(_filterBox, 4, 0)

        _grid.Dock = DockStyle.Fill
        _grid.ReadOnly = True
        _grid.AllowUserToAddRows = False
        _grid.AllowUserToDeleteRows = False
        _grid.AllowUserToResizeRows = False
        _grid.AllowUserToOrderColumns = True
        _grid.RowHeadersVisible = False
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        _grid.MultiSelect = False
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        _grid.BackgroundColor = Color.White
        _grid.BorderStyle = BorderStyle.None
        _grid.ColumnHeadersDefaultCellStyle.Font = _boldFont
        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252)
        _grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText

        AddGridColumn("条码", 11)
        AddGridColumn("格式", 14)
        AddGridColumn("序列号", 13)
        AddGridColumn("磁带厂商", 9)
        AddGridColumn("介质厂商", 9)
        AddGridColumn("磁带生产日期", 13)
        AddGridColumn("加载次数", 9)
        AddGridColumn("累计写入", 10)
        AddGridColumn("累计读取", 10)
        AddGridColumn("完整卷等效量", 15)
        AddGridColumn("分区数", 7)
        AddGridColumn("估算容量损失", 13)
        AddGridColumn("文件大小", 11)
        AddGridColumn("解析状态", 11)
        AddGridColumn("文件路径", 17)
        AddHandler _grid.ColumnHeaderMouseClick, AddressOf GridColumnHeaderClicked

        _progressBar.Visible = False
        _progressBar.Size = New Size(180, 16)
        _statusStrip.Items.Add(_statusLabel)
        _statusStrip.Items.Add(_progressBar)
        _statusLabel.Spring = True
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft
        _statusLabel.Text = "就绪"
        _statusStrip.BackColor = Color.White

        Controls.Add(_grid)
        Controls.Add(topPanel)
        Controls.Add(_statusStrip)
    End Sub

    ''' <summary>打开窗口时自动扫描的文件夹。</summary>
    Public Property InitialFolder As String
        Get
            Return _initialFolder
        End Get
        Set(value As String)
            _initialFolder = value
        End Set
    End Property

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        If Directory.Exists(_initialFolder) Then StartScan(_initialFolder)
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        CancelScan()
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub AddGridColumn(header As String, fillWeight As Single)
        Dim column As New DataGridViewTextBoxColumn() With {
            .HeaderText = header,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Programmatic
        }
        _grid.Columns.Add(column)
    End Sub

    Private Sub ChooseFolderClicked(sender As Object, e As EventArgs)
        Dim initialPath As String = ""
        If Directory.Exists(_scanFolder) Then
            initialPath = _scanFolder
        ElseIf Directory.Exists(_initialFolder) Then
            initialPath = _initialFolder
        End If
        Using dialog As New FolderInputForm(initialPath)
            If dialog.ShowDialog(Me) = DialogResult.OK Then StartScan(dialog.SelectedPath)
        End Using
    End Sub

    Private Sub CancelClicked(sender As Object, e As EventArgs)
        If _scanCancellation IsNot Nothing AndAlso Not _scanCancellation.IsCancellationRequested Then
            _statusLabel.Text = "正在取消扫描..."
            _scanCancellation.Cancel()
        End If
    End Sub

    Private Sub FilterChanged(sender As Object, e As EventArgs)
        ApplyFilter()
    End Sub

    ''' <summary>开始扫描并解析指定文件夹（可在扫描过程中再次调用，会先取消上一次扫描）。</summary>
    Public Sub StartScan(folder As String)
        If String.IsNullOrWhiteSpace(folder) OrElse Not Directory.Exists(folder) Then
            MessageBox.Show(Me, "文件夹不存在：" & folder, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        CancelScan()
        _scanFolder = folder
        _folderLabel.Text = folder
        _toolTip.SetToolTip(_folderLabel, folder)
        _grid.Rows.Clear()
        _entries.Clear()
        _successCount = 0
        _failCount = 0
        _totalCount = 0
        SetScanningState(True)
        _isScanning = True
        _statusLabel.ForeColor = Color.FromArgb(32, 48, 72)
        _statusLabel.Text = "正在扫描文件夹..."
        _progressBar.Style = ProgressBarStyle.Marquee
        _progressBar.Visible = True

        Dim cts As New CancellationTokenSource()
        _scanCancellation = cts
        Dim token As CancellationToken = cts.Token
        Dim scanPath As String = folder
        _scanVersion += 1
        Dim version As Integer = _scanVersion
        Task.Run(Sub() ScanWorker(scanPath, token, version))
    End Sub

    Private Sub ScanWorker(folder As String, token As CancellationToken, version As Integer)
        Try
            Dim files As New List(Of String)()
            EnumerateSupportedFiles(folder, token, files)
            files.Sort(StringComparer.OrdinalIgnoreCase)
            InvokeIfCurrent(version,
                            Sub()
                                _totalCount = files.Count
                                _progressBar.Style = ProgressBarStyle.Continuous
                                _progressBar.Maximum = Math.Max(1, _totalCount)
                                _progressBar.Value = 0
                                _statusLabel.Text = $"扫描到 {_totalCount} 个候选文件，开始解析..."
                            End Sub)
            If token.IsCancellationRequested Then Return

            Dim total As Integer = files.Count
            If total = 0 Then
                InvokeIfCurrent(version, Sub() FinishScan())
                Return
            End If

            Dim batch As New List(Of CmBatchEntry)()
            Dim done As Integer = 0
            For Each file As String In files
                If token.IsCancellationRequested Then Exit For
                Dim entry As CmBatchEntry
                Try
                    entry = CmBatchEntry.FromReport(file, CmReport.ParseFile(file))
                Catch ex As Exception
                    entry = CmBatchEntry.Failed(file, ex.Message)
                End Try
                done += 1
                batch.Add(entry)
                If batch.Count >= 20 OrElse done = total Then
                    Dim batchCopy As List(Of CmBatchEntry) = batch.ToList()
                    batch.Clear()
                    Dim doneCopy As Integer = done
                    InvokeIfCurrent(version,
                                    Sub()
                                        For Each item As CmBatchEntry In batchCopy
                                            AppendEntry(item)
                                        Next
                                        _progressBar.Value = Math.Min(doneCopy, _progressBar.Maximum)
                                        _statusLabel.Text = $"已解析 {doneCopy} / {total} 个文件..."
                                        RefreshRowsAfterBatch()
                                    End Sub)
                End If
            Next
            InvokeIfCurrent(version, Sub() FinishScan())
        Catch ex As Exception
            InvokeIfCurrent(version,
                            Sub()
                                _statusLabel.Text = "扫描失败：" & ex.Message
                                _statusLabel.ForeColor = Color.Firebrick
                                SetScanningState(False)
                                _progressBar.Visible = False
                            End Sub)
        End Try
    End Sub

    Private Shared Sub EnumerateSupportedFiles(folder As String, token As CancellationToken, results As List(Of String))
        Dim pending As New Stack(Of String)()
        pending.Push(folder)
        While pending.Count > 0
            If token.IsCancellationRequested Then Return
            Dim current As String = pending.Pop()
            Try
                For Each file As String In Directory.EnumerateFiles(current)
                    If token.IsCancellationRequested Then Return
                    If IsSupportedFile(file) Then results.Add(file)
                Next
                For Each child As String In Directory.EnumerateDirectories(current)
                    If token.IsCancellationRequested Then Return
                    pending.Push(child)
                Next
            Catch ex As UnauthorizedAccessException
                ' 跳过无权访问的目录
            Catch ex As IOException
                ' 跳过无法读取的目录
            End Try
        End While
    End Sub

    Private Shared Function IsSupportedFile(filePath As String) As Boolean
        Dim extension As String = Path.GetExtension(filePath)
        If String.IsNullOrEmpty(extension) Then Return False
        extension = extension.ToLowerInvariant()
        Return extension = ".cm" OrElse extension = ".txt" OrElse extension = ".bin"
    End Function

    Private Sub AppendEntry(entry As CmBatchEntry)
        _entries.Add(entry)
        If entry.IsSuccess Then
            _successCount += 1
        Else
            _failCount += 1
        End If
        Dim rowIndex As Integer = _grid.Rows.Add(entry.ToRowValues())
        _grid.Rows(rowIndex).Tag = entry
        If Not entry.IsSuccess Then _grid.Rows(rowIndex).DefaultCellStyle.ForeColor = Color.Firebrick
    End Sub

    ''' <summary>扫描批次或排序变化后刷新表格（保持当前排序与筛选）。</summary>
    Private Sub RefreshRowsAfterBatch()
        If _sortColumnIndex >= 0 Then
            SortEntries()
            RebuildGridRows()
        Else
            ApplyFilter()
        End If
    End Sub

    Private Sub GridColumnHeaderClicked(sender As Object, e As DataGridViewCellMouseEventArgs)
        If e.ColumnIndex < 0 Then Return
        If _sortColumnIndex = e.ColumnIndex Then
            _sortAscending = Not _sortAscending
        Else
            _sortColumnIndex = e.ColumnIndex
            _sortAscending = True
        End If
        SortEntries()
        RebuildGridRows()
    End Sub

    Private Sub SortEntries()
        Dim columnIndex As Integer = _sortColumnIndex
        Dim ascending As Boolean = _sortAscending
        _entries.Sort(Function(left As CmBatchEntry, right As CmBatchEntry) CompareEntries(left, right, columnIndex, ascending))
    End Sub

    Private Sub RebuildGridRows()
        _grid.Rows.Clear()
        For Each entry As CmBatchEntry In _entries
            Dim rowIndex As Integer = _grid.Rows.Add(entry.ToRowValues())
            Dim row As DataGridViewRow = _grid.Rows(rowIndex)
            row.Tag = entry
            If Not entry.IsSuccess Then row.DefaultCellStyle.ForeColor = Color.Firebrick
        Next
        ApplyFilter()
        UpdateSortGlyph()
    End Sub

    Private Sub UpdateSortGlyph()
        For Each column As DataGridViewColumn In _grid.Columns
            column.HeaderCell.SortGlyphDirection = SortOrder.None
        Next
        If _sortColumnIndex >= 0 AndAlso _sortColumnIndex < _grid.Columns.Count Then
            _grid.Columns(_sortColumnIndex).HeaderCell.SortGlyphDirection =
                If(_sortAscending, SortOrder.Ascending, SortOrder.Descending)
        End If
    End Sub

    Private Shared Function CompareEntries(left As CmBatchEntry, right As CmBatchEntry,
                                           columnIndex As Integer, ascending As Boolean) As Integer
        Dim result As Integer
        Select Case columnIndex
            Case 6  ' 加载次数
                result = NumericCompare(ParseInteger(left.LoadCount), ParseInteger(right.LoadCount))
            Case 7  ' 累计写入
                result = NumericCompare(ParseSizeToBytes(left.TotalWrite), ParseSizeToBytes(right.TotalWrite))
            Case 8  ' 累计读取
                result = NumericCompare(ParseSizeToBytes(left.TotalRead), ParseSizeToBytes(right.TotalRead))
            Case 9  ' 完整卷等效量
                result = NumericCompare(ParseFirstNumber(left.FullVolumeEquivalents),
                                        ParseFirstNumber(right.FullVolumeEquivalents))
            Case 10 ' 分区数
                result = NumericCompare(ParseInteger(left.PartitionCount), ParseInteger(right.PartitionCount))
            Case 11 ' 估算容量损失
                result = NumericCompare(ParseSizeToBytes(left.EstimatedCapacityLoss),
                                        ParseSizeToBytes(right.EstimatedCapacityLoss))
            Case 12 ' 文件大小
                result = NumericCompare(left.FileSizeBytes, right.FileSizeBytes)
            Case 13 ' 解析状态
                result = left.IsSuccess.CompareTo(right.IsSuccess)
            Case Else
                result = TextCompare(GetSortText(left, columnIndex), GetSortText(right, columnIndex))
        End Select
        If result = 0 Then result = TextCompare(left.FilePath, right.FilePath)
        If Not ascending Then result = -result
        Return result
    End Function

    Private Shared Function GetSortText(entry As CmBatchEntry, columnIndex As Integer) As String
        Select Case columnIndex
            Case 0
                Return NormalizeSortText(entry.Barcode)
            Case 1
                Return NormalizeSortText(entry.MediaFormat)
            Case 2
                Return NormalizeSortText(entry.SerialNumber)
            Case 3
                Return NormalizeSortText(entry.TapeVendor)
            Case 4
                Return NormalizeSortText(entry.MediaVendor)
            Case 5
                Return NormalizeSortText(entry.TapeMfgDate)
            Case Else
                Return entry.FilePath
        End Select
    End Function

    Private Shared Function NormalizeSortText(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return ""
        If String.Equals(value, CmReport.NotAvailableText, StringComparison.OrdinalIgnoreCase) Then Return ""
        Return value
    End Function

    Private Shared Function TextCompare(left As String, right As String) As Integer
        Return String.Compare(left, right, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function NumericCompare(left As Double, right As Double) As Integer
        If left < right Then Return -1
        If left > right Then Return 1
        Return 0
    End Function

    ''' <summary>解析整数显示值；不可用时返回 -1。</summary>
    Private Shared Function ParseInteger(text As String) As Double
        If String.IsNullOrWhiteSpace(text) Then Return -1
        Dim value As Integer
        If Integer.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, value) Then Return value
        Return -1
    End Function

    ''' <summary>解析大小显示值（如 “129.43 GiB”“0 Bytes”）为字节数；不可用时返回 -1。</summary>
    Private Shared Function ParseSizeToBytes(text As String) As Double
        If String.IsNullOrWhiteSpace(text) Then Return -1
        Dim match As Match = Regex.Match(text, "^\s*(-?[0-9]+(?:\.[0-9]+)?)\s*([A-Za-z]+|字节)?")
        If Not match.Success Then Return -1
        Dim value As Double = 0
        If Not Double.TryParse(match.Groups(1).Value, NumberStyles.Float, CultureInfo.InvariantCulture, value) Then Return -1
        Dim unit As String = match.Groups(2).Value.ToUpperInvariant()
        Select Case unit
            Case "", "B", "BYTES", "字节"
                Return value
            Case "KIB", "KB"
                Return value * 1024.0
            Case "MIB", "MB"
                Return value * 1024.0 * 1024.0
            Case "GIB", "GB"
                Return value * 1024.0 * 1024.0 * 1024.0
            Case "TIB", "TB"
                Return value * 1024.0 * 1024.0 * 1024.0 * 1024.0
            Case "PIB", "PB"
                Return value * 1024.0 * 1024.0 * 1024.0 * 1024.0 * 1024.0
            Case Else
                Return -1
        End Select
    End Function

    ''' <summary>解析显示值开头的数字（如 “0.61 FVE (0.24%)”）；不可用时返回 -1。</summary>
    Private Shared Function ParseFirstNumber(text As String) As Double
        If String.IsNullOrWhiteSpace(text) Then Return -1
        Dim match As Match = Regex.Match(text, "-?[0-9]+(?:\.[0-9]+)?")
        If Not match.Success Then Return -1
        Dim value As Double = 0
        If Double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, value) Then Return value
        Return -1
    End Function

    Private Sub FinishScan()
        _progressBar.Visible = False
        SetScanningState(False)
        RefreshRowsAfterBatch()
        _statusLabel.ForeColor = Color.FromArgb(0, 100, 0)
        UpdateSummaryLabel()
    End Sub

    Private Sub SetScanningState(scanning As Boolean)
        _isScanning = scanning
        _chooseButton.Enabled = Not scanning
        _cancelButton.Enabled = scanning
        _filterBox.Enabled = Not scanning
    End Sub

    Private Sub ApplyFilter()
        Dim keyword As String = _filterBox.Text.Trim()
        For Each row As DataGridViewRow In _grid.Rows
            If keyword.Length = 0 Then
                row.Visible = True
            Else
                row.Visible = CellContains(row, 0, keyword) OrElse
                               CellContains(row, 1, keyword) OrElse
                               CellContains(row, 2, keyword) OrElse
                               CellContains(row, 14, keyword)
            End If
        Next
        UpdateSummaryLabel()
    End Sub

    Private Sub UpdateSummaryLabel()
        If _totalCount = 0 Then Return
        Dim shown As Integer = VisibleRowCount()
        If _isScanning Then
            _statusLabel.Text = $"已解析 {_successCount + _failCount} / {_totalCount} 个文件（当前显示 {shown} 行）"
        Else
            _statusLabel.Text = $"完成：共 {_totalCount} 个文件，成功 {_successCount}，失败 {_failCount}（当前显示 {shown} 行）"
        End If
    End Sub

    Private Shared Function CellContains(row As DataGridViewRow, columnIndex As Integer, keyword As String) As Boolean
        If columnIndex >= row.Cells.Count Then Return False
        Dim value As Object = row.Cells(columnIndex).Value
        If value Is Nothing Then Return False
        Return value.ToString().IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Function VisibleRowCount() As Integer
        Dim count As Integer = 0
        For Each row As DataGridViewRow In _grid.Rows
            If row.Visible Then count += 1
        Next
        Return count
    End Function

    Private Sub CancelScan()
        If _scanCancellation IsNot Nothing Then
            Try
                _scanCancellation.Cancel()
            Catch
            End Try
            _scanCancellation = Nothing
        End If
    End Sub

    Private Sub InvokeIfCurrent(version As Integer, action As Action)
        If version <> _scanVersion Then Return
        InvokeOnUi(Sub()
                       If version = _scanVersion Then action()
                   End Sub)
    End Sub

    Private Sub InvokeOnUi(action As Action)
        If IsDisposed OrElse Disposing Then Return
        Try
            If InvokeRequired Then
                Invoke(action)
            Else
                action()
            End If
        Catch ex As ObjectDisposedException
        Catch ex As InvalidOperationException
        End Try
    End Sub
End Class

''' <summary>批量列表中的一行 CM 摘要。</summary>
Friend Class CmBatchEntry
    Public Property FilePath As String = ""
    Public Property FileSizeBytes As Long
    Public Property IsSuccess As Boolean = True
    Public Property ErrorMessage As String = ""

    Public Property Barcode As String = ""
    Public Property MediaFormat As String = ""
    Public Property SerialNumber As String = ""
    Public Property TapeVendor As String = ""
    Public Property MediaVendor As String = ""
    Public Property TapeMfgDate As String = ""
    Public Property LoadCount As String = ""
    Public Property TotalWrite As String = ""
    Public Property TotalRead As String = ""
    Public Property FullVolumeEquivalents As String = ""
    Public Property PartitionCount As String = ""
    Public Property EstimatedCapacityLoss As String = ""

    Public Shared Function FromReport(filePath As String, report As CmReport) As CmBatchEntry
        Dim entry As New CmBatchEntry With {.FilePath = filePath, .IsSuccess = True}
        entry.FileSizeBytes = GetFileSize(filePath)
        entry.Barcode = CmReport.ToDisplayValue(report.Barcode)
        entry.MediaFormat = CmReport.ToDisplayValue(report.MediaFormat)
        entry.SerialNumber = CmReport.ToDisplayValue(report.SerialNumber)
        entry.TapeVendor = CmReport.ToDisplayValue(report.TapeVendor)
        entry.MediaVendor = CmReport.ToDisplayValue(report.MediaVendor)
        entry.TapeMfgDate = CmReport.ToDisplayValue(report.TapeMfgDate)
        entry.LoadCount = CmReport.ToDisplayValue(report.LoadCount)
        entry.TotalWrite = CmReport.ToDisplayValue(report.TotalWrite)
        entry.TotalRead = CmReport.ToDisplayValue(report.TotalRead)
        entry.FullVolumeEquivalents = CmReport.ToDisplayValue(report.FullVolumeEquivalents)
        entry.PartitionCount = CmReport.ToDisplayValue(report.PartitionCount)
        entry.EstimatedCapacityLoss = CmReport.ToDisplayValue(report.EstimatedCapacityLoss)
        Return entry
    End Function

    Public Shared Function Failed(filePath As String, message As String) As CmBatchEntry
        Dim entry As New CmBatchEntry With {.FilePath = filePath, .IsSuccess = False, .ErrorMessage = message}
        entry.FileSizeBytes = GetFileSize(filePath)
        entry.Barcode = CmReport.NotAvailableText
        entry.MediaFormat = CmReport.NotAvailableText
        entry.SerialNumber = CmReport.NotAvailableText
        entry.TapeVendor = CmReport.NotAvailableText
        entry.MediaVendor = CmReport.NotAvailableText
        entry.TapeMfgDate = CmReport.NotAvailableText
        entry.LoadCount = CmReport.NotAvailableText
        entry.TotalWrite = CmReport.NotAvailableText
        entry.TotalRead = CmReport.NotAvailableText
        entry.FullVolumeEquivalents = CmReport.NotAvailableText
        entry.PartitionCount = CmReport.NotAvailableText
        entry.EstimatedCapacityLoss = CmReport.NotAvailableText
        Return entry
    End Function

    Private Shared Function GetFileSize(filePath As String) As Long
        Try
            Return New FileInfo(filePath).Length
        Catch
            Return 0
        End Try
    End Function

    Public Function ToRowValues() As Object()
        Dim sizeText As String = FileSizeBytes.ToString("N0") & " 字节"
        Dim statusText As String = If(IsSuccess, "成功", "失败：" & ErrorMessage)
        Return New Object() {Barcode, MediaFormat, SerialNumber, TapeVendor, MediaVendor, TapeMfgDate,
                             LoadCount, TotalWrite, TotalRead, FullVolumeEquivalents, PartitionCount,
                             EstimatedCapacityLoss, sizeText, statusText, FilePath}
    End Function
End Class

''' <summary>可直接输入/粘贴路径的文件夹选择对话框（系统 FolderBrowserDialog 不支持键入路径）。</summary>
Friend Class FolderInputForm
    Inherits Form

    Private ReadOnly _pathBox As New TextBox()
    Private ReadOnly _browseButton As New Button()
    Private ReadOnly _okButton As New Button()
    Private ReadOnly _cancelButton As New Button()

    Public Sub New(initialPath As String)
        Text = "选择文件夹"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        ClientSize = New Size(640, 132)
        Font = New Font("Microsoft YaHei UI", 9.0F, FontStyle.Regular, GraphicsUnit.Point)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.FromArgb(246, 248, 251)

        Dim hintLabel As New Label() With {
            .Text = "输入或粘贴文件夹路径（会递归扫描其中的 CM 文件），也可以点击「浏览」选择：",
            .AutoSize = False,
            .Location = New Point(14, 12),
            .Size = New Size(612, 22)
        }

        _pathBox.Location = New Point(14, 40)
        _pathBox.Size = New Size(526, 25)
        _pathBox.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        If Not String.IsNullOrWhiteSpace(initialPath) Then _pathBox.Text = initialPath

        _browseButton.Text = "浏览..."
        _browseButton.Location = New Point(548, 39)
        _browseButton.Size = New Size(78, 27)
        _browseButton.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        AddHandler _browseButton.Click, AddressOf BrowseClicked

        _okButton.Text = "确定"
        _okButton.Location = New Point(438, 88)
        _okButton.Size = New Size(92, 30)
        _okButton.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        AddHandler _okButton.Click, AddressOf OkClicked

        _cancelButton.Text = "取消"
        _cancelButton.Location = New Point(534, 88)
        _cancelButton.Size = New Size(92, 30)
        _cancelButton.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        _cancelButton.DialogResult = DialogResult.Cancel

        AcceptButton = _okButton
        CancelButton = _cancelButton

        Controls.Add(hintLabel)
        Controls.Add(_pathBox)
        Controls.Add(_browseButton)
        Controls.Add(_okButton)
        Controls.Add(_cancelButton)
    End Sub

    Public ReadOnly Property SelectedPath As String
        Get
            Return _pathBox.Text.Trim()
        End Get
    End Property

    Private Sub BrowseClicked(sender As Object, e As EventArgs)
        Using dialog As New FolderBrowserDialog()
            dialog.Description = "选择包含 CM 文件的文件夹"
            dialog.ShowNewFolderButton = False
            If Directory.Exists(_pathBox.Text.Trim()) Then dialog.SelectedPath = _pathBox.Text.Trim()
            If dialog.ShowDialog(Me) = DialogResult.OK Then _pathBox.Text = dialog.SelectedPath
        End Using
    End Sub

    Private Sub OkClicked(sender As Object, e As EventArgs)
        Dim path As String = _pathBox.Text.Trim()
        If path.Length = 0 Then
            MessageBox.Show(Me, "请输入文件夹路径。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _pathBox.Focus()
            Return
        End If
        If Not Directory.Exists(path) Then
            MessageBox.Show(Me, "文件夹不存在：" & path, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            _pathBox.Focus()
            Return
        End If
        _pathBox.Text = path
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class
