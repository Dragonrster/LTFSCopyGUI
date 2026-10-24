Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' 带库文件管理器的文件浏览器：以树形目录 + 详细列表显示某盘磁带的 LTFS 索引，
''' 勾选文件后返回待提取列表（界面布局参考主程序的 LTFS 索引浏览器）。
''' </summary>
Public Class ChangerTapeBrowserForm
    Inherits Form

    Private NotInheritable Class BrowserEntry
        Friend Property Item As Object
        Friend Property IsDirectory As Boolean
        Friend Property Name As String = String.Empty
        Friend Property RelativePath As String = String.Empty
        Friend Property Length As Long
        Friend Property CreationTime As String = String.Empty
        Friend Property ModifyTime As String = String.Empty
        Friend Property AccessTime As String = String.Empty
        Friend Property Sha1 As String = String.Empty
        Friend Property ReadOnlyFlag As Boolean
        Friend Property OpenForWriteFlag As Boolean
        Friend Property FileUid As Long
    End Class

    Private ReadOnly _index As ltfsindex
    Private ReadOnly _barcode As String
    Private ReadOnly _volumeLabel As String
    Private ReadOnly _checkedFiles As New Dictionary(Of ltfsindex.file, String)()
    Private ReadOnly _entries As New List(Of BrowserEntry)()

    Private _menuStrip As MenuStrip
    Private _split As SplitContainer
    Private _tree As TreeView
    Private _list As ListView
    Private _pathLabel As Label
    Private _statusLabel As ToolStripStatusLabel
    Private _selectionLabel As ToolStripStatusLabel
    Private _extractButton As Button
    Private _currentNode As TreeNode
    Private _currentRelativePath As String = String.Empty
    Private _sortColumn As Integer = 0
    Private _sortAscending As Boolean = True
    Private _suppressCheckEvents As Boolean

    Public Sub New(index As ltfsindex, barcode As String, volumeLabel As String)
        _index = index
        _barcode = If(barcode, String.Empty)
        _volumeLabel = If(String.IsNullOrWhiteSpace(volumeLabel), _barcode, volumeLabel)
        BuildUi()
        InitializeTree()
    End Sub

    ''' <summary>用户勾选后应提取的文件列表。</summary>
    Friend ReadOnly Property SelectedFiles As List(Of ChangerFileManagerCore.SelectedFile)
        Get
            Dim result As New List(Of ChangerFileManagerCore.SelectedFile)()
            For Each pair In _checkedFiles
                result.Add(New ChangerFileManagerCore.SelectedFile With {
                    .RelativePath = pair.Value,
                    .FileIndex = pair.Key,
                    .Barcode = _barcode
                })
            Next
            result.Sort(Function(x, y) StringComparer.OrdinalIgnoreCase.Compare(x.RelativePath, y.RelativePath))
            Return result
        End Get
    End Property

    Private Sub BuildUi()
        Dim titleLabel As String = If(String.IsNullOrWhiteSpace(_volumeLabel), _barcode, _volumeLabel)
        Name = "ChangerTapeBrowserForm"
        Text = titleLabel & " - 文件浏览器"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1100, 700)
        MinimumSize = New Size(780, 480)
        KeyPreview = True

        Dim menuStrip As New MenuStrip()
        Dim tapeMenu As New ToolStripMenuItem("磁带(&T)")
        tapeMenu.DropDownItems.Add(CreateMenuItem("提取选中文件(&E)", AddressOf ExtractClicked))
        tapeMenu.DropDownItems.Add(New ToolStripSeparator())
        tapeMenu.DropDownItems.Add(CreateMenuItem("全选(&A)", AddressOf SelectAllClicked))
        tapeMenu.DropDownItems.Add(CreateMenuItem("取消全选(&U)", AddressOf UnselectAllClicked))
        tapeMenu.DropDownItems.Add(New ToolStripSeparator())
        tapeMenu.DropDownItems.Add(CreateMenuItem("关闭(&X)", AddressOf CloseClicked))
        Dim dataMenu As New ToolStripMenuItem("数据(&D)")
        dataMenu.DropDownItems.Add(CreateMenuItem("按名称排序(&N)", AddressOf SortByNameClicked))
        dataMenu.DropDownItems.Add(CreateMenuItem("按大小排序(&S)", AddressOf SortBySizeClicked))
        dataMenu.DropDownItems.Add(New ToolStripSeparator())
        dataMenu.DropDownItems.Add(CreateMenuItem("刷新(&R)    F5", AddressOf RefreshClicked))
        menuStrip.Items.Add(tapeMenu)
        menuStrip.Items.Add(dataMenu)
        _menuStrip = menuStrip
        Controls.Add(menuStrip)
        MainMenuStrip = menuStrip

        Dim toolbar As New Panel()
        toolbar.Dock = DockStyle.Top
        toolbar.Height = 44
        toolbar.Padding = New Padding(6, 7, 6, 6)

        Dim buttonsPanel As New FlowLayoutPanel()
        buttonsPanel.Dock = DockStyle.Left
        buttonsPanel.WrapContents = False
        buttonsPanel.AutoSize = False
        buttonsPanel.Width = 384
        buttonsPanel.Padding = New Padding(0, 2, 0, 0)

        _extractButton = New Button()
        _extractButton.Text = "提取选中文件"
        _extractButton.Size = New Size(120, 28)
        _extractButton.Margin = New Padding(0, 0, 6, 0)
        AddHandler _extractButton.Click, AddressOf ExtractClicked
        buttonsPanel.Controls.Add(_extractButton)

        Dim selectAllButton As New Button()
        selectAllButton.Text = "全选"
        selectAllButton.Size = New Size(72, 28)
        selectAllButton.Margin = New Padding(0, 0, 6, 0)
        AddHandler selectAllButton.Click, AddressOf SelectAllClicked
        buttonsPanel.Controls.Add(selectAllButton)

        Dim unselectAllButton As New Button()
        unselectAllButton.Text = "取消全选"
        unselectAllButton.Size = New Size(84, 28)
        unselectAllButton.Margin = New Padding(0, 0, 6, 0)
        AddHandler unselectAllButton.Click, AddressOf UnselectAllClicked
        buttonsPanel.Controls.Add(unselectAllButton)

        Dim refreshButton As New Button()
        refreshButton.Text = "刷新"
        refreshButton.Size = New Size(72, 28)
        refreshButton.Margin = New Padding(0, 0, 6, 0)
        AddHandler refreshButton.Click, AddressOf RefreshClicked
        buttonsPanel.Controls.Add(refreshButton)

        _pathLabel = New Label()
        _pathLabel.AutoSize = False
        _pathLabel.Dock = DockStyle.Fill
        _pathLabel.TextAlign = ContentAlignment.MiddleLeft
        _pathLabel.Padding = New Padding(8, 0, 0, 0)
        Dim pathHost As New Panel()
        pathHost.Dock = DockStyle.Fill
        pathHost.Padding = New Padding(6, 6, 0, 6)
        pathHost.Controls.Add(_pathLabel)
        toolbar.Controls.Add(pathHost)
        toolbar.Controls.Add(buttonsPanel)
        Controls.Add(toolbar)

        Dim statusStrip As New StatusStrip()
        _statusLabel = New ToolStripStatusLabel()
        _statusLabel.Spring = True
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft
        _selectionLabel = New ToolStripStatusLabel()
        _selectionLabel.TextAlign = ContentAlignment.MiddleRight
        statusStrip.Items.Add(_statusLabel)
        statusStrip.Items.Add(_selectionLabel)
        Controls.Add(statusStrip)

        Dim split As New SplitContainer()
        split.Dock = DockStyle.Fill
        split.Orientation = Orientation.Vertical
        split.Size = New Size(1000, 600)
        split.Panel1MinSize = 180
        split.Panel2MinSize = 420
        Controls.Add(split)
        split.BringToFront()
        _split = split

        _tree = New TreeView()
        _tree.Dock = DockStyle.Fill
        _tree.HideSelection = False
        _tree.FullRowSelect = True
        _tree.ShowLines = False
        AddHandler _tree.BeforeExpand, AddressOf TreeBeforeExpand
        AddHandler _tree.AfterSelect, AddressOf TreeAfterSelect
        split.Panel1.Controls.Add(_tree)

        _list = New ListView()
        _list.Dock = DockStyle.Fill
        _list.View = View.Details
        _list.CheckBoxes = True
        _list.FullRowSelect = True
        _list.MultiSelect = True
        _list.HideSelection = False
        _list.ShowItemToolTips = True
        _list.Columns.Add("名字", 280)
        _list.Columns.Add("大小（字节）", 110, HorizontalAlignment.Right)
        _list.Columns.Add("创建时间", 130)
        _list.Columns.Add("修改时间", 130)
        _list.Columns.Add("访问时间", 130)
        _list.Columns.Add("SHA1", 220)
        _list.Columns.Add("只读", 64)
        _list.Columns.Add("已打开", 72)
        _list.Columns.Add("文件 UID", 100)
        AddHandler _list.ColumnClick, AddressOf ListColumnClick
        AddHandler _list.ItemCheck, AddressOf ListItemCheck
        AddHandler _list.ItemChecked, AddressOf ListItemChecked
        AddHandler _list.MouseDoubleClick, AddressOf ListMouseDoubleClick
        split.Panel2.Controls.Add(_list)
        split.SplitterWidth = 6

        AddHandler KeyDown, AddressOf FormKeyDown
        AddHandler Load, AddressOf FormLoad
    End Sub

    Private Sub FormLoad(sender As Object, e As EventArgs)
        If _split Is Nothing Then Return
        Dim available As Integer = _split.Width - _split.Panel2MinSize - _split.SplitterWidth
        Dim desired As Integer = Math.Max(_split.Panel1MinSize, Math.Min(320, available))
        If desired > _split.Panel1MinSize AndAlso desired < _split.Width - _split.Panel2MinSize Then
            Try
                _split.SplitterDistance = desired
            Catch
            End Try
        End If
    End Sub

    Private Shared Function CreateMenuItem(text As String, handler As EventHandler) As ToolStripMenuItem
        Dim item As New ToolStripMenuItem(text)
        AddHandler item.Click, handler
        Return item
    End Function

    Private Sub InitializeTree()
        _tree.BeginUpdate()
        Try
            _tree.Nodes.Clear()
            Dim rootNode As New TreeNode("\\")
            rootNode.Name = String.Empty
            rootNode.ToolTipText = "卷根目录"
            rootNode.Nodes.Add(New TreeNode("加载中...") With {.Name = "__placeholder"})
            _tree.Nodes.Add(rootNode)
            _tree.SelectedNode = rootNode
            rootNode.Expand()
        Finally
            _tree.EndUpdate()
        End Try
        LoadRootChildren(_tree.Nodes(0))
        ShowRootContents()
    End Sub

    Private Sub LoadRootChildren(rootNode As TreeNode)
        Dim cached As Boolean = rootNode.Nodes.Count = 1 AndAlso rootNode.Nodes(0).Name = "__placeholder"
        If Not cached Then Return
        rootNode.Nodes.Clear()
        If _index._directory IsNot Nothing Then
            For Each childDirectory In _index._directory
                If childDirectory Is Nothing Then Continue For
                AddDirectoryNode(rootNode, childDirectory, childDirectory.name)
            Next
        End If
    End Sub

    Private Sub AddDirectoryNode(parentNode As TreeNode, directory As ltfsindex.directory, relativePath As String)
        Dim node As New TreeNode(directory.name)
        node.Name = relativePath
        node.Tag = directory
        node.ToolTipText = "\\" & _volumeLabel & "\" & relativePath
        If HasChildDirectories(directory) Then
            node.Nodes.Add(New TreeNode("加载中...") With {.Name = "__placeholder"})
        End If
        parentNode.Nodes.Add(node)
    End Sub

    Private Shared Function HasChildDirectories(directory As ltfsindex.directory) As Boolean
        If directory Is Nothing Then Return False
        Try
            Return directory.GetLazyDirectDirectoryCount() > 0
        Catch
            Return False
        End Try
    End Function

    Private Sub LoadChildDirectoryNodes(node As TreeNode)
        If node.Nodes.Count <> 1 OrElse node.Nodes(0).Name <> "__placeholder" Then Return
        Dim directory As ltfsindex.directory = TryCast(node.Tag, ltfsindex.directory)
        If directory Is Nothing Then Return
        node.Nodes.Clear()
        Dim count As Integer = directory.GetLazyDirectDirectoryCount()
        For index As Integer = 0 To count - 1
            Dim childDirectory As ltfsindex.directory = directory.GetLazyDirectoryAt(index)
            If childDirectory Is Nothing Then Continue For
            Dim childPath As String = JoinPath(node.Name, childDirectory.name)
            AddDirectoryNode(node, childDirectory, childPath)
        Next
    End Sub

    Private Sub TreeBeforeExpand(sender As Object, e As TreeViewCancelEventArgs)
        LoadChildDirectoryNodes(e.Node)
    End Sub

    Private Sub TreeAfterSelect(sender As Object, e As TreeViewEventArgs)
        If e.Node Is Nothing OrElse e.Node.Name = "__placeholder" Then Return
        _currentNode = e.Node
        _currentRelativePath = e.Node.Name
        Dim directory As ltfsindex.directory = TryCast(e.Node.Tag, ltfsindex.directory)
        If directory Is Nothing Then
            ShowRootContents()
        Else
            ShowDirectoryContents(directory, e.Node.Name)
        End If
    End Sub

    Private Sub ShowRootContents()
        _currentRelativePath = String.Empty
        UpdatePathLabel(String.Empty)
        _entries.Clear()
        If _index._directory IsNot Nothing Then
            For Each childDirectory In _index._directory
                If childDirectory Is Nothing Then Continue For
                _entries.Add(CreateDirectoryEntry(childDirectory, childDirectory.name))
            Next
        End If
        If _index._file IsNot Nothing Then
            For Each childFile In _index._file
                If childFile Is Nothing Then Continue For
                _entries.Add(CreateFileEntry(childFile, childFile.name))
            Next
        End If
        SortEntries()
        PopulateList()
    End Sub

    Private Sub ShowDirectoryContents(directory As ltfsindex.directory, relativePath As String)
        _currentRelativePath = relativePath
        UpdatePathLabel(relativePath)
        _entries.Clear()
        Dim directoryCount As Integer = 0
        Try
            directoryCount = directory.GetLazyDirectDirectoryCount()
        Catch
            directoryCount = 0
        End Try
        For index As Integer = 0 To directoryCount - 1
            Dim childDirectory As ltfsindex.directory = directory.GetLazyDirectoryAt(index)
            If childDirectory Is Nothing Then Continue For
            _entries.Add(CreateDirectoryEntry(childDirectory, JoinPath(relativePath, childDirectory.name)))
        Next
        Dim fileCount As Integer = 0
        Try
            fileCount = directory.GetLazyDirectFileCount()
        Catch
            fileCount = 0
        End Try
        For index As Integer = 0 To fileCount - 1
            Dim childFile As ltfsindex.file = directory.GetLazyFileAt(index)
            If childFile Is Nothing Then Continue For
            _entries.Add(CreateFileEntry(childFile, JoinPath(relativePath, childFile.name)))
        Next
        SortEntries()
        PopulateList()
    End Sub

    Private Shared Function CreateDirectoryEntry(directory As ltfsindex.directory, relativePath As String) As BrowserEntry
        Dim entry As New BrowserEntry()
        entry.IsDirectory = True
        entry.Item = directory
        entry.Name = directory.name & "\"
        entry.RelativePath = relativePath
        entry.CreationTime = directory.creationtime
        entry.ModifyTime = directory.modifytime
        entry.AccessTime = directory.accesstime
        Return entry
    End Function

    Private Shared Function CreateFileEntry(childFile As ltfsindex.file, relativePath As String) As BrowserEntry
        Dim entry As New BrowserEntry()
        entry.IsDirectory = False
        entry.Item = childFile
        entry.Name = childFile.name
        entry.RelativePath = relativePath
        entry.Length = childFile.length
        entry.CreationTime = childFile.creationtime
        entry.ModifyTime = childFile.modifytime
        entry.AccessTime = childFile.accesstime
        entry.ReadOnlyFlag = childFile.[readonly]
        entry.OpenForWriteFlag = childFile.openforwrite
        entry.FileUid = childFile.fileuid
        Try
            entry.Sha1 = childFile.GetXAttr(ltfsindex.file.xattr.HashType.SHA1, True)
        Catch
            entry.Sha1 = String.Empty
        End Try
        Return entry
    End Function

    Private Sub PopulateList()
        _list.BeginUpdate()
        _suppressCheckEvents = True
        Try
            _list.Items.Clear()
            For Each entry In _entries
                Dim item As New ListViewItem(entry.Name)
                item.Tag = entry
                item.SubItems.Add(If(entry.IsDirectory, String.Empty, entry.Length.ToString()))
                item.SubItems.Add(entry.CreationTime)
                item.SubItems.Add(entry.ModifyTime)
                item.SubItems.Add(entry.AccessTime)
                item.SubItems.Add(entry.Sha1)
                item.SubItems.Add(If(entry.IsDirectory, String.Empty, If(entry.ReadOnlyFlag, "True", "False")))
                item.SubItems.Add(If(entry.IsDirectory, String.Empty, If(entry.OpenForWriteFlag, "True", "False")))
                item.SubItems.Add(If(entry.IsDirectory, String.Empty, entry.FileUid.ToString()))
                Dim childFile As ltfsindex.file = If(entry.IsDirectory, Nothing, TryCast(entry.Item, ltfsindex.file))
                If childFile IsNot Nothing AndAlso _checkedFiles.ContainsKey(childFile) Then
                    item.Checked = True
                End If
                item.ToolTipText = entry.RelativePath
                _list.Items.Add(item)
            Next
        Finally
            _suppressCheckEvents = False
            _list.EndUpdate()
        End Try
        UpdateStatus()
    End Sub

    Private Sub UpdatePathLabel(relativePath As String)
        Dim suffix As String = If(String.IsNullOrWhiteSpace(relativePath), String.Empty, "\" & relativePath.TrimStart("\"c))
        _pathLabel.Text = "\\" & _volumeLabel & suffix
    End Sub

    Private Sub UpdateStatus()
        Dim fileCount As Integer = 0
        For Each entry In _entries
            If Not entry.IsDirectory Then fileCount += 1
        Next
        _statusLabel.Text = $"{_volumeLabel}：当前目录 {_entries.Count} 项（文件 {fileCount} 个），已选 {_checkedFiles.Count} 个文件"
        Dim totalBytes As Long = 0
        For Each pair In _checkedFiles
            If pair.Key.length > 0 Then totalBytes += pair.Key.length
        Next
        _selectionLabel.Text = $"已选 {_checkedFiles.Count} 个，合计 {FormatSize(totalBytes)}"
        If _extractButton IsNot Nothing Then
            _extractButton.Enabled = _checkedFiles.Count > 0
        End If
    End Sub

    Private Sub ListItemCheck(sender As Object, e As ItemCheckEventArgs)
        If _suppressCheckEvents Then Return
        Dim entry As BrowserEntry = TryCast(_list.Items(e.Index).Tag, BrowserEntry)
        If entry Is Nothing OrElse entry.IsDirectory Then
            e.NewValue = CheckState.Unchecked
        End If
    End Sub

    Private Sub ListItemChecked(sender As Object, e As ItemCheckedEventArgs)
        If _suppressCheckEvents Then Return
        Dim entry As BrowserEntry = TryCast(e.Item.Tag, BrowserEntry)
        If entry Is Nothing OrElse entry.IsDirectory Then Return
        Dim childFile As ltfsindex.file = TryCast(entry.Item, ltfsindex.file)
        If childFile Is Nothing Then Return
        If e.Item.Checked Then
            _checkedFiles(childFile) = entry.RelativePath
        Else
            _checkedFiles.Remove(childFile)
        End If
        UpdateStatus()
    End Sub

    Private Sub SelectAllClicked(sender As Object, e As EventArgs)
        _suppressCheckEvents = True
        Try
            For index As Integer = 0 To _list.Items.Count - 1
                Dim entry As BrowserEntry = TryCast(_list.Items(index).Tag, BrowserEntry)
                If entry Is Nothing OrElse entry.IsDirectory Then Continue For
                Dim childFile As ltfsindex.file = TryCast(entry.Item, ltfsindex.file)
                If childFile Is Nothing Then Continue For
                _list.Items(index).Checked = True
                _checkedFiles(childFile) = entry.RelativePath
            Next
        Finally
            _suppressCheckEvents = False
        End Try
        UpdateStatus()
    End Sub

    Private Sub UnselectAllClicked(sender As Object, e As EventArgs)
        _checkedFiles.Clear()
        _suppressCheckEvents = True
        Try
            For index As Integer = 0 To _list.Items.Count - 1
                If _list.Items(index).Checked Then _list.Items(index).Checked = False
            Next
        Finally
            _suppressCheckEvents = False
        End Try
        UpdateStatus()
    End Sub

    Private Sub RefreshClicked(sender As Object, e As EventArgs)
        If _currentNode IsNot Nothing Then
            If TryCast(_currentNode.Tag, ltfsindex.directory) IsNot Nothing Then
                ShowDirectoryContents(DirectCast(_currentNode.Tag, ltfsindex.directory), _currentNode.Name)
            Else
                ShowRootContents()
            End If
        Else
            ShowRootContents()
        End If
    End Sub

    Private Sub ExtractClicked(sender As Object, e As EventArgs)
        If _checkedFiles.Count = 0 Then
            MessageBox.Show(Me, "请先勾选要提取的文件。", "提取选中文件", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub CloseClicked(sender As Object, e As EventArgs)
        DialogResult = DialogResult.Cancel
        Close()
    End Sub

    Private Sub SortByNameClicked(sender As Object, e As EventArgs)
        _sortColumn = 0
        _sortAscending = True
        SortEntries()
        PopulateList()
    End Sub

    Private Sub SortBySizeClicked(sender As Object, e As EventArgs)
        _sortColumn = 1
        _sortAscending = False
        SortEntries()
        PopulateList()
    End Sub

    Private Sub ListColumnClick(sender As Object, e As ColumnClickEventArgs)
        If _sortColumn = e.Column Then
            _sortAscending = Not _sortAscending
        Else
            _sortColumn = e.Column
            _sortAscending = True
        End If
        SortEntries()
        PopulateList()
    End Sub

    Private Sub SortEntries()
        Dim comparison As Comparison(Of BrowserEntry) = BuildComparison()
        _entries.Sort(comparison)
    End Sub

    Private Function BuildComparison() As Comparison(Of BrowserEntry)
        Dim column As Integer = _sortColumn
        Dim ascending As Boolean = _sortAscending
        Return Function(left As BrowserEntry, right As BrowserEntry) As Integer
                   If left.IsDirectory <> right.IsDirectory Then Return If(left.IsDirectory, -1, 1)
                   Dim result As Integer
                   Select Case column
                       Case 1
                           result = left.Length.CompareTo(right.Length)
                       Case 2
                           result = StringComparer.OrdinalIgnoreCase.Compare(left.CreationTime, right.CreationTime)
                       Case 3
                           result = StringComparer.OrdinalIgnoreCase.Compare(left.ModifyTime, right.ModifyTime)
                       Case 4
                           result = StringComparer.OrdinalIgnoreCase.Compare(left.AccessTime, right.AccessTime)
                       Case 5
                           result = StringComparer.OrdinalIgnoreCase.Compare(left.Sha1, right.Sha1)
                       Case 6
                           result = left.ReadOnlyFlag.CompareTo(right.ReadOnlyFlag)
                       Case 7
                           result = left.OpenForWriteFlag.CompareTo(right.OpenForWriteFlag)
                       Case 8
                           result = left.FileUid.CompareTo(right.FileUid)
                       Case Else
                           result = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name)
                   End Select
                   If Not ascending Then result = -result
                   Return result
               End Function
    End Function

    Private Sub ListMouseDoubleClick(sender As Object, e As MouseEventArgs)
        If _list.SelectedItems.Count = 0 Then Return
        Dim entry As BrowserEntry = TryCast(_list.SelectedItems(0).Tag, BrowserEntry)
        If entry Is Nothing OrElse Not entry.IsDirectory Then Return
        Dim childDirectory As ltfsindex.directory = TryCast(entry.Item, ltfsindex.directory)
        If childDirectory Is Nothing Then Return
        Dim targetNode As TreeNode = EnsureTreeNode(entry.RelativePath)
        If targetNode IsNot Nothing Then
            _tree.SelectedNode = targetNode
            targetNode.Expand()
        Else
            ShowDirectoryContents(childDirectory, entry.RelativePath)
        End If
    End Sub

    Private Function EnsureTreeNode(relativePath As String) As TreeNode
        If String.IsNullOrEmpty(relativePath) Then Return _tree.Nodes(0)
        Dim node As TreeNode = _tree.Nodes(0)
        Dim currentPath As String = String.Empty
        For Each segment In relativePath.Split(New Char() {"\"c}, StringSplitOptions.RemoveEmptyEntries)
            currentPath = JoinPath(currentPath, segment)
            LoadChildDirectoryNodes(node)
            Dim nextNode As TreeNode = Nothing
            For Each childNode As TreeNode In node.Nodes
                If String.Equals(childNode.Name, currentPath, StringComparison.OrdinalIgnoreCase) Then
                    nextNode = childNode
                    Exit For
                End If
            Next
            If nextNode Is Nothing Then Return Nothing
            node = nextNode
        Next
        Return node
    End Function

    Private Sub FormKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.F5 Then
            RefreshClicked(sender, EventArgs.Empty)
            e.Handled = True
        ElseIf e.KeyCode = Keys.Escape Then
            CloseClicked(sender, EventArgs.Empty)
            e.Handled = True
        End If
    End Sub

    Private Shared Function JoinPath(parentPath As String, childName As String) As String
        Dim parent As String = If(parentPath, String.Empty).TrimEnd("\"c)
        Dim child As String = If(childName, String.Empty).Trim("\"c)
        If parent.Length = 0 Then Return child
        If child.Length = 0 Then Return parent
        Return parent & "\" & child
    End Function

    Private Shared Function FormatSize(byteCount As Long) As String
        If byteCount <= 0 Then Return "0 Bytes"
        If byteCount < 1024 Then Return byteCount.ToString() & " Bytes"
        Dim units As String() = {"KiB", "MiB", "GiB", "TiB", "PiB", "EiB"}
        Dim value As Double = byteCount
        Dim unitIndex As Integer = -1
        Do
            value /= 1024.0
            unitIndex += 1
        Loop While value >= 1024.0 AndAlso unitIndex < units.Length - 1
        Return value.ToString("0.##") & " " & units(unitIndex)
    End Function
End Class
