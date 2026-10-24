Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports LTFSCopyGUI.TapeUtils

''' <summary>
''' 带库文件管理器（纯代码构造 UI，无 Designer/resx）。
''' 列出带库中的满槽磁带，读取/解析 LTFS 索引，浏览索引内文件并提取勾选的文件。
''' 菜单接线由 ChangerTool 负责；本窗体也可通过无参构造反射实例化用于 UI 冒烟。
''' </summary>
Friend Class ChangerFileManagerForm
    Inherits Form

    Private NotInheritable Class ExtractionJob
        Friend Property Slot As ChangerFileManagerCore.TapeSlot
        Friend Property Files As List(Of ChangerFileManagerCore.SelectedFile)
    End Class

    Private ReadOnly _core As ChangerFileManagerCore
    Private ReadOnly _slots As New List(Of ChangerFileManagerCore.TapeSlot)()
    Private ReadOnly _selections As New Dictionary(Of String, List(Of ChangerFileManagerCore.SelectedFile))(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _buttons As New List(Of Button)()

    Private _grid As DataGridView
    Private _logBox As TextBox
    Private _statusLabel As Label
    Private _busy As Boolean

    ''' <summary>无参构造：不绑定带库，仅用于反射/冒烟测试。</summary>
    Public Sub New()
        Me.New(Nothing, String.Empty)
    End Sub

    ''' <summary>绑定带库构造。</summary>
    Public Sub New(changer As MediumChanger, changerPath As String)
        MyBase.New()
        _core = New ChangerFileManagerCore(changer, changerPath, AddressOf AppendLogThreadSafe, AddressOf SetProgressThreadSafe)
        BuildUserInterface()
        UpdateButtonState()
        AddHandler Me.Load, AddressOf ChangerFileManagerForm_Load
        AddHandler Me.FormClosing, AddressOf ChangerFileManagerForm_FormClosing
    End Sub

    Private Sub BuildUserInterface()
        Me.Text = "LTFS 带库文件管理器"
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Width = 1000
        Me.Height = 640
        Me.MinimumSize = New Size(800, 520)

        Dim rootLayout As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(6)
        }
        rootLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 180.0F))

        Dim buttonPanel As New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .Margin = New Padding(0, 0, 0, 4)
        }
        Dim refreshButton As New Button With {.Text = "刷新带库", .AutoSize = True, .Margin = New Padding(0, 0, 6, 0), .Padding = New Padding(6, 3, 6, 3)}
        Dim readSelectedButton As New Button With {.Text = "读取所选索引", .AutoSize = True, .Margin = New Padding(0, 0, 6, 0), .Padding = New Padding(6, 3, 6, 3)}
        Dim readAllButton As New Button With {.Text = "读取全部索引", .AutoSize = True, .Margin = New Padding(0, 0, 6, 0), .Padding = New Padding(6, 3, 6, 3)}
        Dim browseButton As New Button With {.Text = "浏览文件", .AutoSize = True, .Margin = New Padding(0, 0, 6, 0), .Padding = New Padding(6, 3, 6, 3)}
        Dim extractButton As New Button With {.Text = "提取选中文件", .AutoSize = True, .Margin = New Padding(0, 0, 6, 0), .Padding = New Padding(6, 3, 6, 3)}
        _buttons.Add(refreshButton)
        _buttons.Add(readSelectedButton)
        _buttons.Add(readAllButton)
        _buttons.Add(browseButton)
        _buttons.Add(extractButton)
        buttonPanel.Controls.Add(refreshButton)
        buttonPanel.Controls.Add(readSelectedButton)
        buttonPanel.Controls.Add(readAllButton)
        buttonPanel.Controls.Add(browseButton)
        buttonPanel.Controls.Add(extractButton)

        _grid = New DataGridView With {
            .Dock = DockStyle.Fill,
            .ReadOnly = True,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .RowHeadersVisible = False,
            .MultiSelect = True,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            .ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            .BackgroundColor = SystemColors.Window,
            .BorderStyle = BorderStyle.FixedSingle,
            .Margin = New Padding(0, 0, 0, 4)
        }
        Dim slotColumn As New DataGridViewTextBoxColumn With {.HeaderText = "槽位", .Name = "colSlot", .Width = 110, .ReadOnly = True}
        Dim barcodeColumn As New DataGridViewTextBoxColumn With {.HeaderText = "条码", .Name = "colBarcode", .Width = 140, .ReadOnly = True}
        Dim schemaColumn As New DataGridViewTextBoxColumn With {.HeaderText = "本地索引", .Name = "colSchema", .ReadOnly = True, .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 45.0F}
        Dim countColumn As New DataGridViewTextBoxColumn With {.HeaderText = "文件数", .Name = "colFileCount", .Width = 70, .ReadOnly = True, .AutoSizeMode = DataGridViewAutoSizeColumnMode.None}
        Dim statusColumn As New DataGridViewTextBoxColumn With {.HeaderText = "状态", .Name = "colStatus", .ReadOnly = True, .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 40.0F}
        _grid.Columns.AddRange(New DataGridViewColumn() {slotColumn, barcodeColumn, schemaColumn, countColumn, statusColumn})

        Dim bottomPanel As New Panel With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0)
        }
        _statusLabel = New Label With {
            .Dock = DockStyle.Bottom,
            .Height = 24,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Text = "就绪",
            .Padding = New Padding(4, 0, 4, 0),
            .BorderStyle = BorderStyle.FixedSingle
        }
        _logBox = New TextBox With {
            .Dock = DockStyle.Fill,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Vertical,
            .WordWrap = True,
            .BackColor = SystemColors.Window,
            .BorderStyle = BorderStyle.FixedSingle
        }
        bottomPanel.Controls.Add(_statusLabel)
        bottomPanel.Controls.Add(_logBox)

        rootLayout.Controls.Add(buttonPanel, 0, 0)
        rootLayout.Controls.Add(_grid, 0, 1)
        rootLayout.Controls.Add(bottomPanel, 0, 2)
        Me.Controls.Add(rootLayout)

        AddHandler refreshButton.Click, AddressOf BtnRefresh_Click
        AddHandler readSelectedButton.Click, AddressOf BtnReadSelected_Click
        AddHandler readAllButton.Click, AddressOf BtnReadAll_Click
        AddHandler browseButton.Click, AddressOf BtnBrowse_Click
        AddHandler extractButton.Click, AddressOf BtnExtract_Click
    End Sub

#Region "窗体事件"

    Private Async Sub ChangerFileManagerForm_Load(sender As Object, e As EventArgs)
        SetBusy(False)
        If Not _core.HasChanger Then
            AppendLog("未绑定磁带库（本窗体以无参构造创建），磁带操作不可用。")
            SetStatus("未绑定带库")
            UpdateButtonState()
            Return
        End If
        Try
            Await RefreshTapesAsync()
        Catch ex As Exception
            AppendLog($"刷新带库失败：{ex.Message}")
            SetStatus("刷新带库失败")
        End Try
    End Sub

    Private Sub ChangerFileManagerForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        If _busy Then
            e.Cancel = True
            MessageBox.Show(Me, "操作正在进行中，请等待完成后再关闭窗口。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Async Sub BtnRefresh_Click(sender As Object, e As EventArgs)
        If _busy Then Return
        Try
            Await RefreshTapesAsync()
        Catch ex As Exception
            AppendLog($"刷新带库失败：{ex.Message}")
        End Try
    End Sub

    Private Async Sub BtnReadSelected_Click(sender As Object, e As EventArgs)
        If _busy Then Return
        Try
            Await ReadIndexesAsync(onlySelected:=True)
        Catch ex As Exception
            AppendLog($"读取索引失败：{ex.Message}")
        End Try
    End Sub

    Private Async Sub BtnReadAll_Click(sender As Object, e As EventArgs)
        If _busy Then Return
        Try
            Await ReadIndexesAsync(onlySelected:=False)
        Catch ex As Exception
            AppendLog($"读取索引失败：{ex.Message}")
        End Try
    End Sub

    Private Async Sub BtnBrowse_Click(sender As Object, e As EventArgs)
        If _busy Then Return
        Try
            Await BrowseFilesAsync()
        Catch ex As Exception
            AppendLog($"浏览文件失败：{ex.Message}")
        End Try
    End Sub

    Private Async Sub BtnExtract_Click(sender As Object, e As EventArgs)
        If _busy Then Return
        Try
            Await ExtractFilesAsync()
        Catch ex As Exception
            AppendLog($"提取文件失败：{ex.Message}")
        End Try
    End Sub

#End Region

#Region "刷新带库"

    Private Async Function RefreshTapesAsync() As Task
        If _busy Then Return
        If Not _core.HasChanger Then
            AppendLog("未绑定磁带库，无法刷新带库。")
            Return
        End If
        SetBusy(True, "正在扫描带库...")
        Try
            AppendLog("正在扫描带库元素...")
            Dim slots As List(Of ChangerFileManagerCore.TapeSlot) = Await Task.Run(Function() _core.RefreshTapes())
            _slots.Clear()
            If slots IsNot Nothing Then _slots.AddRange(slots)
            FillGrid()
            If _slots.Count = 0 Then
                AppendLog("没有发现满槽磁带。")
                SetStatus("没有发现满槽磁带")
                Return
            End If
            SetStatus($"已发现 {_slots.Count} 盘磁带，正在解析本地索引...")
            Await Task.Run(Sub()
                               For Each slot In _slots
                                   If String.IsNullOrWhiteSpace(slot.LocalSchemaPath) Then Continue For
                                   _core.LoadLocalIndexForSlot(slot)
                               Next
                           End Sub)
            UpdateAllRows()
            SetStatus($"就绪：{_slots.Count} 盘磁带")
        Finally
            SetBusy(False)
        End Try
    End Function

    Private Sub FillGrid()
        _grid.Rows.Clear()
        For Each slot In _slots
            Dim rowIndex As Integer = _grid.Rows.Add()
            Dim row As DataGridViewRow = _grid.Rows(rowIndex)
            row.Tag = slot
            UpdateRow(row, slot)
        Next
    End Sub

    Private Sub UpdateAllRows()
        For Each row As DataGridViewRow In _grid.Rows
            Dim slot As ChangerFileManagerCore.TapeSlot = TryCast(row.Tag, ChangerFileManagerCore.TapeSlot)
            If slot IsNot Nothing Then UpdateRow(row, slot)
        Next
    End Sub

    Private Sub UpdateRow(slot As ChangerFileManagerCore.TapeSlot)
        For Each row As DataGridViewRow In _grid.Rows
            If ReferenceEquals(row.Tag, slot) Then
                UpdateRow(row, slot)
                Return
            End If
        Next
    End Sub

    Private Shared Sub UpdateRow(row As DataGridViewRow, slot As ChangerFileManagerCore.TapeSlot)
        If row Is Nothing OrElse slot Is Nothing Then Return
        If row.DataGridView IsNot Nothing AndAlso row.DataGridView.Columns.Count < 5 Then Return
        row.Cells(0).Value = slot.SlotDisplayText & If(slot.IsImportExport, " 导入/导出", " 存储槽")
        row.Cells(1).Value = GetEffectiveBarcode(slot)
        Dim schemaPath As String = slot.LocalSchemaPath
        row.Cells(2).Value = If(String.IsNullOrWhiteSpace(schemaPath), "无", IO.Path.GetFileName(schemaPath))
        row.Cells(2).ToolTipText = If(schemaPath, String.Empty)
        row.Cells(3).Value = If(slot.FileCount >= 0, slot.FileCount.ToString(), "-")
        row.Cells(4).Value = slot.Status
    End Sub

#End Region

#Region "读取索引"

    Private Async Function ReadIndexesAsync(onlySelected As Boolean) As Task
        If _busy Then Return
        If Not _core.HasChanger Then
            AppendLog("未绑定磁带库，无法读取索引。")
            Return
        End If
        Dim targets As List(Of ChangerFileManagerCore.TapeSlot)
        If onlySelected Then
            targets = GetSelectedSlots()
            If targets.Count = 0 Then
                AppendLog("请先在列表中选择要读取索引的磁带。")
                Return
            End If
        Else
            targets = New List(Of ChangerFileManagerCore.TapeSlot)(_slots)
        End If
        If targets.Count = 0 Then
            AppendLog("列表中没有磁带，请先刷新带库。")
            Return
        End If
        If Not onlySelected Then
            Dim answer As DialogResult = MessageBox.Show(Me, $"将要依次读取 {targets.Count} 盘磁带的索引，过程中需要占用驱动器且耗时较长，是否继续？", "读取全部索引", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If answer <> DialogResult.Yes Then Return
        End If

        SetBusy(True, "正在读取索引...")
        Dim successCount As Integer = 0
        Dim failedCount As Integer = 0
        Try
            For Each currentSlot In targets
                Dim slot As ChangerFileManagerCore.TapeSlot = currentSlot
                AppendLog($">>> [{slot.Barcode}] 开始读取索引...")
                slot.Status = "正在读取索引"
                UpdateRow(slot)
                Try
                    Dim index As ltfsindex = Await Task.Run(Function() _core.ReadIndexFromTape(slot))
                    If index IsNot Nothing Then
                        successCount += 1
                    Else
                        failedCount += 1
                        slot.Status = "读取索引失败"
                    End If
                Catch ex As Exception
                    failedCount += 1
                    slot.Status = "读取索引失败"
                    AppendLog($"[{slot.Barcode}] 读取索引失败：{ex.Message}")
                End Try
                UpdateRow(slot)
            Next
        Finally
            SetBusy(False)
            UpdateAllRows()
        End Try
        SetStatus($"读取索引完成：成功 {successCount} 盘，失败 {failedCount} 盘")
        AppendLog($"读取索引完成：成功 {successCount} 盘，失败 {failedCount} 盘。")
    End Function

#End Region

#Region "浏览文件"

    Private Async Function BrowseFilesAsync() As Task
        If _busy Then Return
        If Not _core.HasChanger Then
            AppendLog("未绑定磁带库，无法浏览文件。")
            Return
        End If
        Dim slot As ChangerFileManagerCore.TapeSlot = GetSingleSelectedSlot()
        If slot Is Nothing Then Return
        If String.IsNullOrWhiteSpace(slot.Barcode) Then
            AppendLog("所选磁带的条码未知，无法匹配本地索引。")
            Return
        End If
        SetBusy(True, "正在加载本地索引...")
        Dim index As ltfsindex = Nothing
        Try
            index = Await Task.Run(Function() _core.GetCachedOrLocalIndex(slot.Barcode))
        Finally
            SetBusy(False)
        End Try
        If index Is Nothing Then
            AppendLog($"[{slot.Barcode}] 没有可用的本地索引，请先读取该磁带的索引。")
            SetStatus($"[{slot.Barcode}] 没有可用的本地索引")
            Return
        End If

        SetStatus($"[{slot.Barcode}] 请在文件浏览器中勾选要提取的文件")
        Dim editableIndex As ltfsindex = index
        Dim transaction As LazySelectionTransaction = index.BeginSelectionTransaction()
        If transaction Is Nothing Then editableIndex = index.Clone()
        Dim accepted As Boolean = False
        Try
            accepted = (FileBrowser.ShowDialog(editableIndex) = DialogResult.OK)
        Finally
            If transaction IsNot Nothing Then
                If accepted Then
                    transaction.Commit()
                Else
                    transaction.Rollback()
                End If
            End If
        End Try
        If Not accepted Then
            AppendLog("已取消文件选择。")
            SetStatus("就绪")
            Return
        End If

        Dim files As List(Of ChangerFileManagerCore.SelectedFile) = _core.CollectSelectedFiles(editableIndex, slot.Barcode)
        _selections(slot.Barcode) = files
        slot.Status = $"已选择 {files.Count} 个文件（待提取）"
        UpdateRow(slot)
        AppendLog($"[{slot.Barcode}] 已选择 {files.Count} 个文件。")
        SetStatus($"[{slot.Barcode}] 已选择 {files.Count} 个文件")
    End Function

#End Region

#Region "提取文件"

    Private Async Function ExtractFilesAsync() As Task
        If _busy Then Return
        If Not _core.HasChanger Then
            AppendLog("未绑定磁带库，无法提取文件。")
            Return
        End If
        If _selections.Count = 0 Then
            AppendLog("请先通过「浏览文件」选择要提取的文件。")
            Return
        End If

        Dim jobs As New List(Of ExtractionJob)()
        Dim totalFiles As Integer = 0
        For Each entry In _selections
            Dim slot As ChangerFileManagerCore.TapeSlot = FindSlotByBarcode(entry.Key)
            If slot Is Nothing Then
                AppendLog($"[{entry.Key}] 该磁带不在当前带库列表中，已跳过 {entry.Value.Count} 个文件。")
                Continue For
            End If
            If entry.Value Is Nothing OrElse entry.Value.Count = 0 Then Continue For
            jobs.Add(New ExtractionJob With {.Slot = slot, .Files = entry.Value})
            totalFiles += entry.Value.Count
        Next
        If jobs.Count = 0 Then
            AppendLog("没有可提取的文件。")
            Return
        End If

        Dim targetRoot As String = SelectTargetFolder()
        If String.IsNullOrWhiteSpace(targetRoot) Then Return

        SetBusy(True, "正在检查可用驱动器...")
        Dim mappedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)) = Nothing
        Try
            mappedDrives = Await Task.Run(Function() _core.GetMappedDriveList())
        Finally
            SetBusy(False)
        End Try
        If mappedDrives Is Nothing OrElse mappedDrives.Count = 0 Then
            AppendLog("没有可用的磁带驱动器（或无法映射到本机磁带设备），无法提取。")
            SetStatus("没有可用的磁带驱动器")
            Return
        End If

        Dim selectedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)) = mappedDrives
        If jobs.Count > 1 AndAlso mappedDrives.Count > 1 Then
            selectedDrives = SelectExtractionDrives(mappedDrives)
            If selectedDrives Is Nothing OrElse selectedDrives.Count = 0 Then
                AppendLog("已取消提取（未选择驱动器）。")
                SetStatus("就绪")
                Return
            End If
        End If

        Dim jobCount As Integer = jobs.Count
        Dim successFiles As Integer = 0
        Dim failedTapes As Integer = 0
        SetBusy(True, "正在提取文件...")
        Try
            Await Task.Run(Sub()
                               Dim queue As New List(Of ExtractionJob)(jobs)
                               Dim queueLock As New Object()
                               Dim workerCount As Integer = Math.Min(selectedDrives.Count, queue.Count)
                               Dim workers As New List(Of Task)()
                               For driveIndex As Integer = 0 To workerCount - 1
                                   Dim driveInfo As Tuple(Of MediumChanger.Element, BlockDevice) = selectedDrives(driveIndex)
                                   workers.Add(Task.Run(Sub()
                                                            Do
                                                                Dim job As ExtractionJob = DequeueExtractionJob(queue, queueLock)
                                                                If job Is Nothing Then Exit Do
                                                                Dim slot As ChangerFileManagerCore.TapeSlot = job.Slot
                                                                Dim tapeBarcode As String = GetEffectiveBarcode(slot)
                                                                Dim tapeTarget As String = targetRoot
                                                                If jobCount > 1 Then
                                                                    tapeTarget = IO.Path.Combine(targetRoot, SanitizeFolderName(tapeBarcode))
                                                                End If
                                                                Dim driveText As String = $"0x{driveInfo.Item1.ElementAddress.ToString("X4")} ({driveInfo.Item2.DevicePath})"
                                                                AppendLogThreadSafe($">>> [{tapeBarcode}] 开始提取 {job.Files.Count} 个文件到：{tapeTarget}（驱动器 {driveText}）")
                                                                slot.Status = "正在提取文件"
                                                                UpdateRowThreadSafe(slot)
                                                                Try
                                                                    Dim extracted As Integer = _core.ExtractFiles(slot, job.Files, tapeTarget, driveInfo.Item1)
                                                                    Interlocked.Add(successFiles, extracted)
                                                                    slot.Status = $"提取完成：{extracted} 个文件"
                                                                    AppendLogThreadSafe($"[{tapeBarcode}] 提取完成：{extracted} 个文件。")
                                                                Catch ex As Exception
                                                                    Interlocked.Increment(failedTapes)
                                                                    slot.Status = "提取失败"
                                                                    AppendLogThreadSafe($"[{tapeBarcode}] 提取失败：{ex.Message}")
                                                                End Try
                                                                UpdateRowThreadSafe(slot)
                                                            Loop
                                                        End Sub))
                               Next
                               Task.WaitAll(workers.ToArray())
                           End Sub)
        Finally
            SetBusy(False)
            UpdateAllRows()
        End Try
        Dim summary As String = $"提取结束：成功 {successFiles}/{totalFiles} 个文件"
        If failedTapes > 0 Then summary &= $"，{failedTapes} 盘失败"
        SetStatus(summary)
        AppendLog(summary & "。")
    End Function

    Private Function SelectTargetFolder() As String
        Using dialog As New FolderBrowserDialog()
            dialog.Description = "选择提取文件的目标文件夹"
            dialog.ShowNewFolderButton = True
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return Nothing
            Return dialog.SelectedPath
        End Using
    End Function

    Private Shared Function SanitizeFolderName(name As String) As String
        Dim source As String = If(String.IsNullOrWhiteSpace(name), "Unknown", name.Trim())
        Dim invalidChars As Char() = IO.Path.GetInvalidFileNameChars()
        Dim builder As New Text.StringBuilder(source.Length)
        For Each c As Char In source
            If Array.IndexOf(invalidChars, c) >= 0 Then
                builder.Append("_"c)
            Else
                builder.Append(c)
            End If
        Next
        Dim result As String = builder.ToString().Trim()
        If result.Length = 0 Then Return "Unknown"
        Return result
    End Function

#End Region

#Region "UI 辅助"

    Private Shared Function GetEffectiveBarcode(slot As ChangerFileManagerCore.TapeSlot) As String
        If slot Is Nothing Then Return "Unknown"
        If Not String.IsNullOrWhiteSpace(slot.ResolvedBarcode) Then Return slot.ResolvedBarcode
        If Not String.IsNullOrWhiteSpace(slot.Barcode) Then Return slot.Barcode
        Return "Unknown"
    End Function

    Private Shared Function DequeueExtractionJob(jobs As List(Of ExtractionJob), queueLock As Object) As ExtractionJob
        SyncLock queueLock
            If jobs.Count = 0 Then Return Nothing
            Dim job As ExtractionJob = jobs(0)
            jobs.RemoveAt(0)
            Return job
        End SyncLock
    End Function

    Private Function SelectExtractionDrives(mappedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice))) As List(Of Tuple(Of MediumChanger.Element, BlockDevice))
        Using dialog As New ChangerDriveSelectorForm(mappedDrives, "选择参与提取的驱动器", "选择参与本次提取的驱动器（至少一个；选中的驱动器会并行提取）：", "确定", "取消")
            If dialog.ShowDialog(Me) = DialogResult.OK Then Return dialog.SelectedDrives
        End Using
        Return Nothing
    End Function

    Private Sub UpdateRowThreadSafe(slot As ChangerFileManagerCore.TapeSlot)
        If slot Is Nothing OrElse Me.IsDisposed Then Return
        If Me.InvokeRequired Then
            Try
                Me.BeginInvoke(New Action(Of ChangerFileManagerCore.TapeSlot)(AddressOf UpdateRowThreadSafe), slot)
            Catch ex As Exception
                Return
            End Try
            Return
        End If
        UpdateRow(slot)
    End Sub
    Private Function FindSlotByBarcode(barcode As String) As ChangerFileManagerCore.TapeSlot
        For Each slot In _slots
            If String.Equals(slot.Barcode, barcode, StringComparison.OrdinalIgnoreCase) Then Return slot
        Next
        Return Nothing
    End Function

    Private Function GetSelectedSlots() As List(Of ChangerFileManagerCore.TapeSlot)
        Dim result As New List(Of ChangerFileManagerCore.TapeSlot)()
        For Each row As DataGridViewRow In _grid.SelectedRows
            Dim slot As ChangerFileManagerCore.TapeSlot = TryCast(row.Tag, ChangerFileManagerCore.TapeSlot)
            If slot IsNot Nothing Then result.Add(slot)
        Next
        result.Sort(Function(a, b) a.SlotAddress.CompareTo(b.SlotAddress))
        Return result
    End Function

    Private Function GetSingleSelectedSlot() As ChangerFileManagerCore.TapeSlot
        Dim slots As List(Of ChangerFileManagerCore.TapeSlot) = GetSelectedSlots()
        If slots.Count = 0 Then
            AppendLog("请先在列表中选择一盘磁带。")
            Return Nothing
        End If
        If slots.Count > 1 Then
            AppendLog("「浏览文件」一次只支持一盘磁带，已使用列表中的第一盘。")
        End If
        Return slots(0)
    End Function

    Private Sub SetBusy(busy As Boolean, Optional status As String = Nothing)
        _busy = busy
        UpdateButtonState()
        If Not String.IsNullOrWhiteSpace(status) Then SetStatus(status)
        Cursor = If(busy, Cursors.WaitCursor, Cursors.Default)
    End Sub

    Private Sub UpdateButtonState()
        Dim enabled As Boolean = (Not _busy) AndAlso _core.HasChanger
        For Each button In _buttons
            button.Enabled = enabled
        Next
        _grid.Enabled = Not _busy
    End Sub

    Private Sub SetStatus(text As String)
        If _statusLabel Is Nothing Then Return
        _statusLabel.Text = text
    End Sub

    Private Sub SetProgressThreadSafe(message As String)
        If String.IsNullOrWhiteSpace(message) OrElse Me.IsDisposed Then Return
        If Me.InvokeRequired Then
            Try
                Me.BeginInvoke(New Action(Of String)(AddressOf SetProgressThreadSafe), message)
            Catch ex As Exception
                Return
            End Try
            Return
        End If
        SetStatus(message)
    End Sub

    Private Sub AppendLogThreadSafe(message As String)
        If String.IsNullOrWhiteSpace(message) OrElse Me.IsDisposed Then Return
        If Me.InvokeRequired Then
            Try
                Me.BeginInvoke(New Action(Of String)(AddressOf AppendLogThreadSafe), message)
            Catch ex As Exception
                Return
            End Try
            Return
        End If
        AppendLog(message)
    End Sub

    Private Sub AppendLog(message As String)
        If _logBox Is Nothing OrElse _logBox.IsDisposed Then Return
        If _logBox.TextLength > 400000 Then _logBox.Clear()
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}")
    End Sub

#End Region

End Class
