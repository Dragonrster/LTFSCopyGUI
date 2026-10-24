Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Threading
Imports LTFSCopyGUI.TapeUtils

''' <summary>
''' 带库文件管理器核心逻辑。
''' 负责枚举带库中的满槽磁带、映射磁带驱动器、读取并缓存 LTFS 索引、提取索引中勾选的文件。
''' 本类不包含任何 UI；耗时方法均设计为可在后台线程调用，执行进度通过构造时传入的回调输出。
''' 所有携带磁带的操作统一遵循：MoveMedium 到驱动器 → OpenTapeDrive → 操作 →
''' LoadEject(Unthread) → CloseTapeDrive → MoveMedium 回原槽位，并在异常时保证磁带归位。
''' </summary>
Friend NotInheritable Class ChangerFileManagerCore

    ''' <summary>带库中的一个满槽磁带（位于存储槽或导入/导出槽）。</summary>
    Friend Class TapeSlot
        Friend Property Element As MediumChanger.Element
        Friend Property IsImportExport As Boolean
        Friend Property DriveElement As MediumChanger.Element
        Friend Property Drive As BlockDevice
        ''' <summary>本地最新索引文件路径（可能是 .schema 或 .schema.zst）。</summary>
        Friend Property LocalSchemaPath As String
        ''' <summary>已解析的本地索引文件中的文件数；-1 表示尚未统计。</summary>
        Friend Property FileCount As Integer = -1
        Friend Property Status As String = String.Empty

        Friend ReadOnly Property SlotAddress As UInt16
            Get
                If Element Is Nothing Then Return CUShort(0)
                Return Element.ElementAddress
            End Get
        End Property

        Friend ReadOnly Property Barcode As String
            Get
                If Element Is Nothing Then Return String.Empty
                Dim value As String = Element.PrimaryVolumeTagInformation
                If value Is Nothing Then Return String.Empty
                Return value.Trim()
            End Get
        End Property

        ''' <summary>从磁带标签/卷内解析出的完整条码（可能含代数后缀）；尚未读取时为 Nothing。</summary>
        Friend Property ResolvedBarcode As String

        Friend ReadOnly Property SlotDisplayText As String
            Get
                Return $"0x{SlotAddress.ToString("X4")}"
            End Get
        End Property
    End Class

    ''' <summary>用户在文件浏览器中勾选的一个待提取文件。</summary>
    Friend Class SelectedFile
        ''' <summary>相对于卷根的路径（用于写入目标文件夹）。</summary>
        Friend Property RelativePath As String = String.Empty
        Friend Property FileIndex As ltfsindex.file
        Friend Property Barcode As String = String.Empty
    End Class

    Private NotInheritable Class DriveCandidate
        Friend Property Element As MediumChanger.Element
        Friend Property Device As BlockDevice
    End Class

    Private NotInheritable Class TapeLayout
        Friend Property ExtraPartitionCount As Byte
        Friend Property IndexPartition As Byte
        Friend Property DataPartition As Byte
        Friend Property BlockSize As Integer
        Friend Property Label As ltfslabel
        Friend Property Vol1Header As String = String.Empty
    End Class

    Private NotInheritable Class ExtractionPlanItem
        Friend Property Item As SelectedFile
        Friend Property Extent As ltfsindex.file.extent
        Friend Property ExtentIndex As Integer
        Friend Property PartitionNumber As Byte
        Friend Property FileExtentTotal As Integer
    End Class

    Private NotInheritable Class ExtractionPlan
        Friend Property Items As New List(Of ExtractionPlanItem)
        Friend Property EmptyFiles As New List(Of SelectedFile)
    End Class

    Private NotInheritable Class ExtractionPlanComparer
        Implements IComparer(Of ExtractionPlanItem)

        Public Function Compare(x As ExtractionPlanItem, y As ExtractionPlanItem) As Integer Implements IComparer(Of ExtractionPlanItem).Compare
            Dim result As Integer = x.PartitionNumber.CompareTo(y.PartitionNumber)
            If result <> 0 Then Return result
            result = x.Extent.startblock.CompareTo(y.Extent.startblock)
            If result <> 0 Then Return result
            result = x.Extent.byteoffset.CompareTo(y.Extent.byteoffset)
            If result <> 0 Then Return result
            result = x.Extent.fileoffset.CompareTo(y.Extent.fileoffset)
            If result <> 0 Then Return result
            result = String.Compare(x.Item.RelativePath, y.Item.RelativePath, StringComparison.OrdinalIgnoreCase)
            If result <> 0 Then Return result
            Return x.ExtentIndex.CompareTo(y.ExtentIndex)
        End Function
    End Class

    ''' <summary>一次已装载的磁带会话；Dispose 时卸带、关闭句柄并把磁带移回原槽位。</summary>
    Private NotInheritable Class MountedTape
        Implements IDisposable

        Private ReadOnly _changerPath As String
        Private ReadOnly _slot As TapeSlot
        Private ReadOnly _driveElement As MediumChanger.Element
        Private ReadOnly _log As Action(Of String)
        Private _handle As IntPtr = IntPtr.Zero
        Private _tapeMoved As Boolean
        Private _disposed As Boolean

        Friend Sub New(changerPath As String, slot As TapeSlot, driveElement As MediumChanger.Element, log As Action(Of String))
            _changerPath = changerPath
            _slot = slot
            _driveElement = driveElement
            _log = log
        End Sub

        Friend Property Handle As IntPtr
            Get
                Return _handle
            End Get
            Set(value As IntPtr)
                _handle = value
            End Set
        End Property

        Friend Sub MarkTapeMoved()
            _tapeMoved = True
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _disposed Then Return
            _disposed = True
            Dim handle As IntPtr = _handle
            _handle = IntPtr.Zero
            If handle <> IntPtr.Zero Then
                Try
                    TapeUtils.AllowMediumRemoval(handle)
                Catch ex As Exception
                    WriteLog($"允许磁带移除失败：{ex.Message}")
                End Try
                Try
                    TapeUtils.LoadEject(handle, TapeUtils.LoadOption.Unthread)
                Catch ex As Exception
                    WriteLog($"卸载（Unthread）磁带失败：{ex.Message}")
                End Try
                Try
                    TapeUtils.CloseTapeDrive(handle)
                Catch ex As Exception
                    WriteLog($"关闭磁带驱动器句柄失败：{ex.Message}")
                End Try
            End If
            If _tapeMoved AndAlso _driveElement IsNot Nothing AndAlso _slot IsNot Nothing Then
                Try
                    Dim sense(63) As Byte
                    MediumChanger.MoveMedium(_changerPath, CUInt(_driveElement.ElementAddress), CUInt(_slot.SlotAddress), sense, _driveElement.LUN)
                    If GetSenseKey(sense) = 0 Then
                        WriteLog($"磁带已归位：{_slot.Barcode} -> 槽位 {_slot.SlotDisplayText}")
                    Else
                        WriteLog($"磁带归位可能失败（槽位 {_slot.SlotDisplayText}）：{TapeUtils.ParseSenseData(sense)}")
                    End If
                Catch ex As Exception
                    WriteLog($"磁带归位失败（槽位 {_slot.SlotDisplayText}）：{ex.Message}")
                End Try
            End If
        End Sub

        Private Sub WriteLog(message As String)
            If _log IsNot Nothing Then _log(message)
        End Sub
    End Class

    Private ReadOnly _changer As MediumChanger
    Private ReadOnly _changerPath As String
    Private ReadOnly _log As Action(Of String)
    Private ReadOnly _progress As Action(Of String)
    Private ReadOnly _cacheLock As New Object()
    Private ReadOnly _indexCache As New Dictionary(Of String, ltfsindex)(StringComparer.OrdinalIgnoreCase)

    Friend Sub New(changer As MediumChanger, changerPath As String, Optional log As Action(Of String) = Nothing, Optional progress As Action(Of String) = Nothing)
        _changer = changer
        Dim resolvedPath As String = changerPath
        If String.IsNullOrWhiteSpace(resolvedPath) AndAlso changer IsNot Nothing Then
            resolvedPath = $"\\.\CHANGER{changer.DevIndex}"
            If changer.device IsNot Nothing Then resolvedPath = changer.device.DevicePath
        End If
        _changerPath = resolvedPath
        _log = log
        _progress = progress
    End Sub

    Friend ReadOnly Property ChangerPath As String
        Get
            Return _changerPath
        End Get
    End Property

    Friend ReadOnly Property HasChanger As Boolean
        Get
            Return _changer IsNot Nothing
        End Get
    End Property

#Region "带库枚举"

    ''' <summary>刷新带库元素状态并返回所有满槽磁带。</summary>
    Friend Function RefreshTapes() As List(Of TapeSlot)
        EnsureChanger()
        If TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream Then
            LogMessage("提示：当前驱动类型为 TapeStream（磁带镜像仿真），物理带库操作可能不可用。")
        End If
        RefreshChangerElements()
        Dim elements As List(Of MediumChanger.Element) = _changer.Elements
        If elements Is Nothing Then elements = New List(Of MediumChanger.Element)()

        Dim emptyDrives As New List(Of MediumChanger.Element)
        Dim occupiedDrives As New List(Of MediumChanger.Element)
        For Each element In elements
            If element Is Nothing Then Continue For
            If element.ElementTypeCode <> MediumChanger.Element.ElementTypeCodes.DataTransferElement Then Continue For
            If element.Full Then
                occupiedDrives.Add(element)
            Else
                emptyDrives.Add(element)
            End If
        Next
        If occupiedDrives.Count > 0 Then
            LogMessage($"警告：有 {occupiedDrives.Count} 个驱动器内仍装载着磁带，这些磁带不会出现在列表中。")
        End If

        Dim candidates As New List(Of DriveCandidate)
        For Each drive As MediumChanger.Element In emptyDrives
            Dim device As BlockDevice = Nothing
            If TryMapDriveElement(drive, device) Then
                candidates.Add(New DriveCandidate With {.Element = drive, .Device = device})
            End If
        Next
        If candidates.Count = 0 Then
            LogMessage("警告：没有找到可映射到本机磁带设备的空闲驱动器，无法装载磁带。")
        End If

        Dim result As New List(Of TapeSlot)
        For Each element In elements
            If element Is Nothing OrElse Not element.Full Then Continue For
            Dim code As MediumChanger.Element.ElementTypeCodes = element.ElementTypeCode
            If code <> MediumChanger.Element.ElementTypeCodes.StorageElement AndAlso
               code <> MediumChanger.Element.ElementTypeCodes.ImportExportElement Then
                Continue For
            End If
            Dim slot As New TapeSlot With {
                .Element = element,
                .IsImportExport = (code = MediumChanger.Element.ElementTypeCodes.ImportExportElement)
            }
            If candidates.Count > 0 Then
                slot.DriveElement = candidates(0).Element
                slot.Drive = candidates(0).Device
            End If
            slot.LocalSchemaPath = TryFindNewestLocalSchemaPath(slot.Barcode)
            If String.IsNullOrWhiteSpace(slot.Barcode) Then
                slot.Status = "条码未知"
            ElseIf String.IsNullOrWhiteSpace(slot.LocalSchemaPath) Then
                slot.Status = "无本地索引"
            Else
                slot.Status = "本地索引待解析"
            End If
            result.Add(slot)
        Next
        result.Sort(AddressOf CompareTapeSlots)
        LogMessage($"带库扫描完成：{result.Count} 盘满槽磁带，{candidates.Count} 个可用驱动器。")
        Return result
    End Function

    Private Shared Function CompareTapeSlots(x As TapeSlot, y As TapeSlot) As Integer
        Return x.SlotAddress.CompareTo(y.SlotAddress)
    End Function

    Private Sub RefreshChangerElements()
        _changer.RefreshElementStatus()
        If _changer.Elements Is Nothing OrElse _changer.Elements.Count = 0 Then
            _changer.RefreshElementStatus(IgnoreLUN0:=False)
        End If
    End Sub

    ''' <summary>按驱动器元素标识（序列号）匹配本机磁带设备。</summary>
    Private Shared Function TryMapDriveElement(driveElement As MediumChanger.Element, ByRef device As BlockDevice) As Boolean
        device = Nothing
        If driveElement Is Nothing Then Return False
        Dim identifier As String = driveElement.Identifier
        If String.IsNullOrWhiteSpace(identifier) Then Return False
        identifier = identifier.Replace("  ", " ").Replace("  ", " ").Replace("  ", " ").Trim()
        If identifier.Length = 0 Then Return False
        Dim devices As List(Of BlockDevice) = TapeUtils.GetTapeDriveList()
        If devices Is Nothing Then Return False
        For Each candidate In devices
            If candidate Is Nothing Then Continue For
            Dim serial As String = candidate.SerialNumber
            If String.IsNullOrWhiteSpace(serial) Then Continue For
            If identifier.EndsWith(serial.Trim(), StringComparison.OrdinalIgnoreCase) Then
                device = candidate
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary>返回当前可用（空闲且能映射到本机磁带设备）的驱动器列表，用于多驱动器并行提取。</summary>
    Friend Function GetMappedDriveList() As List(Of Tuple(Of MediumChanger.Element, BlockDevice))
        Dim result As New List(Of Tuple(Of MediumChanger.Element, BlockDevice))()
        EnsureChanger()
        RefreshChangerElements()
        If _changer.Elements Is Nothing Then Return result
        Dim usedDevicePaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each element In _changer.Elements
            If element Is Nothing Then Continue For
            If element.ElementTypeCode <> MediumChanger.Element.ElementTypeCodes.DataTransferElement Then Continue For
            If element.Full Then Continue For
            Dim device As BlockDevice = Nothing
            If Not TryMapDriveElement(element, device) Then Continue For
            If usedDevicePaths.Add(device.DevicePath) Then
                result.Add(New Tuple(Of MediumChanger.Element, BlockDevice)(element, device))
            End If
        Next
        Return result
    End Function

    Private Function FindAvailableDriveElement() As MediumChanger.Element
        RefreshChangerElements()
        If _changer.Elements Is Nothing Then Return Nothing
        For Each element In _changer.Elements
            If element Is Nothing Then Continue For
            If element.ElementTypeCode <> MediumChanger.Element.ElementTypeCodes.DataTransferElement Then Continue For
            If element.Full Then Continue For
            Dim device As BlockDevice = Nothing
            If TryMapDriveElement(element, device) Then Return element
        Next
        Return Nothing
    End Function

#End Region

#Region "本地索引"

    ''' <summary>在 schemaPath\&lt;条码&gt;\ 下查找最新的 .schema（优先）或 .schema.zst。</summary>
    Friend Function FindNewestLocalSchemaPath(barcode As String) As String
        If String.IsNullOrWhiteSpace(barcode) Then Return Nothing
        Dim schemaRoot As String = My.Settings.schemaPath
        If String.IsNullOrWhiteSpace(schemaRoot) Then Return Nothing
        Dim safeBarcode As String = SafeDirectoryName(barcode)
        If String.IsNullOrWhiteSpace(safeBarcode) Then Return Nothing
        Dim barcodeDir As String = Path.Combine(schemaRoot, safeBarcode)
        If Directory.Exists(barcodeDir) Then
            Dim found As String = FindNewestSchemaInDirectory(New DirectoryInfo(barcodeDir))
            If Not String.IsNullOrWhiteSpace(found) Then Return found
        End If
        Return FindNewestLocalSchemaByPrefix(schemaRoot, safeBarcode)
    End Function

    ''' <summary>在指定目录中查找最新的 .schema 或 .schema.zst。</summary>
    Private Shared Function FindNewestSchemaInDirectory(directory As DirectoryInfo) As String
        If directory Is Nothing OrElse Not directory.Exists Then Return Nothing
        Dim newest As FileInfo = FindNewestFile(directory, "*.schema")
        If newest Is Nothing Then newest = FindNewestFile(directory, "*.schema.zst")
        If newest Is Nothing Then Return Nothing
        Return newest.FullName
    End Function

    ''' <summary>按条码前缀匹配 schema 目录（处理带库 PVTI 缺少代数后缀的情况）。</summary>
    Private Shared Function FindNewestLocalSchemaByPrefix(schemaRoot As String, safeBarcode As String) As String
        Try
            Dim root As New DirectoryInfo(schemaRoot)
            If Not root.Exists OrElse String.IsNullOrWhiteSpace(safeBarcode) Then Return Nothing
            Dim newest As FileInfo = Nothing
            For Each folder As DirectoryInfo In root.GetDirectories(safeBarcode & "*", SearchOption.TopDirectoryOnly)
                Dim found As FileInfo = FindNewestFile(folder, "*.schema")
                If found Is Nothing Then found = FindNewestFile(folder, "*.schema.zst")
                If found Is Nothing Then Continue For
                If newest Is Nothing OrElse found.LastWriteTimeUtc > newest.LastWriteTimeUtc Then newest = found
            Next
            Return If(newest Is Nothing, Nothing, newest.FullName)
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>从 schemaPath\&lt;条码&gt;[\\Load]\\file 路径反推条码目录名。</summary>
    Private Shared Function TryGetSchemaFolderBarcode(schemaFilePath As String) As String
        Try
            If String.IsNullOrWhiteSpace(schemaFilePath) Then Return Nothing
            Dim directoryPath As String = Path.GetDirectoryName(schemaFilePath)
            If String.IsNullOrWhiteSpace(directoryPath) Then Return Nothing
            If String.Equals(Path.GetFileName(directoryPath), "Load", StringComparison.OrdinalIgnoreCase) Then
                directoryPath = Path.GetDirectoryName(directoryPath)
            End If
            If String.IsNullOrWhiteSpace(directoryPath) Then Return Nothing
            Return Path.GetFileName(directoryPath)
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Shared Function FindNewestFile(directory As DirectoryInfo, pattern As String) As FileInfo
        Dim files As FileInfo() = directory.GetFiles(pattern, SearchOption.AllDirectories)
        Dim newest As FileInfo = Nothing
        For Each file In files
            If file Is Nothing Then Continue For
            If newest Is Nothing Then
                newest = file
            ElseIf file.LastWriteTimeUtc > newest.LastWriteTimeUtc Then
                newest = file
            End If
        Next
        Return newest
    End Function

    Private Function TryFindNewestLocalSchemaPath(barcode As String) As String
        Try
            Return FindNewestLocalSchemaPath(barcode)
        Catch ex As Exception
            LogMessage($"扫描本地索引失败：{ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>加载指定条码的本地索引（优先使用内存缓存）。索引可能很大，建议在后台线程调用。</summary>
    Friend Function LoadLocalIndex(barcode As String) As ltfsindex
        If String.IsNullOrWhiteSpace(barcode) Then Return Nothing
        Dim cachedIndex As ltfsindex = GetCachedIndex(barcode)
        If cachedIndex IsNot Nothing Then Return cachedIndex
        Dim schemaPath As String = TryFindNewestLocalSchemaPath(barcode)
        If String.IsNullOrWhiteSpace(schemaPath) Then Return Nothing
        Dim loadPath As String = schemaPath
        If schemaPath.EndsWith(".zst", StringComparison.OrdinalIgnoreCase) Then
            loadPath = DecompressSchema(schemaPath)
            If loadPath Is Nothing Then Return Nothing
        End If
        Dim index As ltfsindex = TryLoadIndexFile(loadPath)
        If index Is Nothing Then
            LogMessage($"本地索引无法解析：{schemaPath}")
            Return Nothing
        End If
        SetCachedIndex(barcode, index)
        Return index
    End Function

    ''' <summary>解析某个槽位已有的本地索引并统计文件数（带异常捕获）。</summary>
    Friend Sub LoadLocalIndexForSlot(slot As TapeSlot)
        If slot Is Nothing Then Return
        Try
            Dim index As ltfsindex = LoadLocalIndex(slot.Barcode)
            If index Is Nothing Then
                If Not String.IsNullOrWhiteSpace(slot.LocalSchemaPath) Then slot.Status = "本地索引无法解析"
                Return
            End If
            slot.FileCount = CountFiles(index)
            slot.Status = "本地索引已加载"
            If String.IsNullOrWhiteSpace(slot.ResolvedBarcode) Then
                Dim resolvedBarcode As String = TryGetSchemaFolderBarcode(slot.LocalSchemaPath)
                If Not String.IsNullOrWhiteSpace(resolvedBarcode) Then slot.ResolvedBarcode = resolvedBarcode
            End If
        Catch ex As Exception
            slot.Status = "本地索引解析失败"
            LogMessage($"[{slot.Barcode}] 解析本地索引失败：{ex.Message}")
        End Try
    End Sub

    Private Shared Function TryLoadIndexFile(filePath As String) As ltfsindex
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return Nothing
        Try
            Dim info As New FileInfo(filePath)
            If info.Length < 64 Then Return Nothing
            If Not LooksLikeLTFSIndex(filePath) Then Return Nothing
            Return ltfsindex.FromSchFile(filePath)
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>轻量校验文件是否为 LTFS 索引 XML，避免 FromSchFile 对损坏文件弹窗。</summary>
    Private Shared Function LooksLikeLTFSIndex(filePath As String) As Boolean
        Try
            Dim buffer(4095) As Byte
            Dim read As Integer = 0
            Using stream As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                read = stream.Read(buffer, 0, buffer.Length)
            End Using
            If read <= 0 Then Return False
            Dim text As String = Encoding.UTF8.GetString(buffer, 0, read)
            Return text.IndexOf("<ltfsindex", StringComparison.OrdinalIgnoreCase) >= 0
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Function DecompressSchema(zstPath As String) As String
        Try
            Dim targetPath As String = LazySchemaStore.CreateTempFilePath("changer-index")
            Using inStream As New FileStream(zstPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Using zstdStream As New ZstdSharp.DecompressionStream(inStream)
                    Using outStream As New FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None)
                        zstdStream.CopyTo(outStream)
                    End Using
                End Using
            End Using
            Return targetPath
        Catch ex As Exception
            LogMessage($"解压本地索引失败：{ex.Message}")
            Return Nothing
        End Try
    End Function

    Friend Function GetCachedIndex(barcode As String) As ltfsindex
        If String.IsNullOrWhiteSpace(barcode) Then Return Nothing
        SyncLock _cacheLock
            Dim index As ltfsindex = Nothing
            If _indexCache.TryGetValue(barcode.Trim(), index) Then Return index
        End SyncLock
        Return Nothing
    End Function

    Friend Sub SetCachedIndex(barcode As String, index As ltfsindex)
        If String.IsNullOrWhiteSpace(barcode) OrElse index Is Nothing Then Return
        SyncLock _cacheLock
            _indexCache(barcode.Trim()) = index
        End SyncLock
    End Sub

    Friend Function GetCachedOrLocalIndex(barcode As String) As ltfsindex
        Dim index As ltfsindex = GetCachedIndex(barcode)
        If index IsNot Nothing Then Return index
        Return LoadLocalIndex(barcode)
    End Function

    ''' <summary>统计一个索引中的文件总数。</summary>
    Friend Function CountFiles(index As ltfsindex) As Integer
        If index Is Nothing Then Return 0
        Dim count As Integer = 0
        Dim rootFiles As List(Of ltfsindex.file) = index._file
        If rootFiles IsNot Nothing Then count += rootFiles.Count
        Dim rootDirectories As List(Of ltfsindex.directory) = index._directory
        If rootDirectories IsNot Nothing Then
            For Each directory In rootDirectories
                CountFilesInDirectory(directory, count)
            Next
        End If
        Return count
    End Function

    Private Shared Sub CountFilesInDirectory(directory As ltfsindex.directory, ByRef count As Integer)
        If directory Is Nothing Then Return
        For Each childFile In directory.EnumerateLazyFiles()
            count += 1
        Next
        For Each childDirectory In directory.EnumerateLazyDirectories()
            CountFilesInDirectory(childDirectory, count)
        Next
    End Sub

#End Region

#Region "从磁带读取索引"

    ''' <summary>从一盘磁带读取 LTFS 索引并保存到 schemaPath\&lt;条码&gt;\Load\。磁带会自动装载并归位。</summary>
    Friend Function ReadIndexFromTape(slot As TapeSlot) As ltfsindex
        If slot Is Nothing Then Throw New ArgumentNullException(NameOf(slot))
        EnsureChanger()
        Using mount As MountedTape = MountTape(slot)
            Dim layout As TapeLayout = ReadTapeLayout(mount.Handle)
            LocateIndexFileMark(mount.Handle, layout)
            Dim tempPath As String = LazySchemaStore.CreateTempFilePath("changer-index-read")
            If Not TapeUtils.ReadToFileMark(mount.Handle, tempPath, layout.BlockSize) Then
                Throw New IOException($"从磁带读取索引失败（{slot.Barcode}）。")
            End If
            If Not LooksLikeLTFSIndex(tempPath) Then
                Try
                    File.Delete(tempPath)
                Catch ex As Exception
                    LogMessage($"删除无效索引临时文件失败：{ex.Message}")
                End Try
                Throw New IOException($"磁带上的索引数据无效（{slot.Barcode}）。")
            End If
            Dim index As ltfsindex = ltfsindex.FromSchFile(tempPath)
            If index Is Nothing Then
                Throw New IOException($"解析磁带索引失败（{slot.Barcode}）。")
            End If
            Dim barcodeOnTape As String = TapeUtils.ReadBarcode(mount.Handle)
            Dim finalBarcode As String = If(String.IsNullOrWhiteSpace(barcodeOnTape), slot.Barcode, barcodeOnTape.Trim())
            slot.ResolvedBarcode = finalBarcode
            Dim savedPath As String = SaveIndexCopy(finalBarcode, index, tempPath)
            If String.IsNullOrWhiteSpace(savedPath) AndAlso File.Exists(tempPath) Then
                Try
                    File.Delete(tempPath)
                Catch ex As Exception
                    LogMessage($"删除临时索引文件失败：{ex.Message}")
                End Try
            End If
            slot.LocalSchemaPath = If(savedPath, slot.LocalSchemaPath)
            SetCachedIndex(finalBarcode, index)
            If Not String.Equals(finalBarcode, slot.Barcode, StringComparison.OrdinalIgnoreCase) Then
                SetCachedIndex(slot.Barcode, index)
            End If
            slot.FileCount = CountFiles(index)
            slot.Status = "索引已读取"
            LogMessage($"[{finalBarcode}] 索引读取成功，共 {slot.FileCount} 个文件。")
            Return index
        End Using
    End Function

    ''' <summary>把临时索引移动到 schemaPath 下的 Load 目录，并把旧的 .schema 压缩为 .zst。</summary>
    Private Function SaveIndexCopy(barcode As String, index As ltfsindex, tempPath As String) As String
        If String.IsNullOrWhiteSpace(tempPath) OrElse Not File.Exists(tempPath) Then Return Nothing
        Dim schemaRoot As String = My.Settings.schemaPath
        If String.IsNullOrWhiteSpace(schemaRoot) Then Return Nothing
        Dim baseBarcode As String = If(String.IsNullOrWhiteSpace(barcode), "Unknown", barcode.Trim())
        Dim targetDir As String = Path.Combine(schemaRoot, SafeDirectoryName(baseBarcode), "Load")
        Directory.CreateDirectory(targetDir)
        Dim fileName As String = baseBarcode
        If index IsNot Nothing AndAlso String.IsNullOrWhiteSpace(fileName) Then
            fileName = index.volumeuuid.ToString()
        End If
        Dim latestPath As String = Path.Combine(targetDir, $"LTFSIndex_Load_{SafeFileName(fileName)}_{DateTime.Now:yyyyMMdd_HHmmss_fffffff}.schema")
        File.Move(tempPath, latestPath)
        Try
            For Each oldSchema In Directory.GetFiles(targetDir, "*.schema", SearchOption.TopDirectoryOnly)
                If String.Equals(oldSchema, latestPath, StringComparison.OrdinalIgnoreCase) Then Continue For
                Dim zstPath As String = oldSchema & ".zst"
                Using inStream As New FileStream(oldSchema, FileMode.Open, FileAccess.Read, FileShare.Read)
                    Using outStream As New FileStream(zstPath, FileMode.Create, FileAccess.Write, FileShare.None)
                        Using zstdStream As New ZstdSharp.CompressionStream(outStream, 9)
                            inStream.CopyTo(zstdStream)
                        End Using
                    End Using
                End Using
                File.Delete(oldSchema)
            Next
        Catch ex As Exception
            LogMessage($"压缩旧索引失败：{ex.Message}")
        End Try
        LogMessage($"索引已保存：{latestPath}")
        Return latestPath
    End Function

    ''' <summary>读取 VOL1、索引分区标签以及物理分区映射。</summary>
    Private Function ReadTapeLayout(handle As IntPtr) As TapeLayout
        Dim layout As New TapeLayout()
        Dim modeData As Byte() = TapeUtils.ModeSense(handle, &H11)
        If modeData IsNot Nothing AndAlso modeData.Length >= 4 Then layout.ExtraPartitionCount = modeData(3)
        Dim indexPartition As Byte = 0
        Dim dataPartition As Byte = 0

        TapeUtils.Locate(handle, 0UL, Math.Min(layout.ExtraPartitionCount, indexPartition), TapeUtils.LocateDestType.Block)
        Dim senseData As Byte() = Nothing
        Dim vol1Data As Byte() = TapeUtils.ReadBlock(handle, senseData)
        If vol1Data Is Nothing Then vol1Data = Array.Empty(Of Byte)()
        Dim header As String = Encoding.ASCII.GetString(vol1Data)
        layout.Vol1Header = header
        Dim vol1Legal As Boolean = header.Length = 80
        If vol1Legal Then vol1Legal = header.StartsWith("VOL1", StringComparison.Ordinal)
        If vol1Legal Then vol1Legal = header.Substring(24, 4) = "LTFS"
        If Not vol1Legal Then
            Throw New IOException($"该磁带不是有效的 LTFS 磁带（VOL1 校验失败）。{TapeUtils.ParseSenseData(senseData)}")
        End If

        TapeUtils.Locate(handle, 1UL, Math.Min(layout.ExtraPartitionCount, indexPartition), TapeUtils.LocateDestType.FileMark)
        TapeUtils.ReadFileMark(handle)
        Dim labelText As String = Encoding.UTF8.GetString(TapeUtils.ReadToFileMark(handle))
        Dim volumeLabel As ltfslabel = ltfslabel.FromXML(labelText)
        If volumeLabel Is Nothing Then Throw New IOException("无法解析磁带上的 LTFS 索引分区标签。")
        If volumeLabel.blocksize <= 0 Then Throw New IOException("LTFS 标签中的 blocksize 无效。")
        TapeUtils.SetBlockSize(handle, volumeLabel.blocksize)

        Dim position As New TapeUtils.PositionData(handle)
        If volumeLabel.location.partition = volumeLabel.partitions.data Then
            dataPartition = position.PartitionNumber
            indexPartition = CByte((CInt(dataPartition) + 1) Mod 2)
            If layout.ExtraPartitionCount > 0 Then
                indexPartition = 255
                TapeUtils.Locate(handle, 1UL, indexPartition, TapeUtils.LocateDestType.FileMark)
                TapeUtils.ReadFileMark(handle)
                labelText = Encoding.UTF8.GetString(TapeUtils.ReadToFileMark(handle))
                volumeLabel = ltfslabel.FromXML(labelText)
                If volumeLabel Is Nothing Then Throw New IOException("无法从索引分区解析 LTFS 标签。")
                If volumeLabel.blocksize <= 0 Then Throw New IOException("LTFS 标签中的 blocksize 无效。")
                TapeUtils.SetBlockSize(handle, volumeLabel.blocksize)
            End If
        Else
            indexPartition = position.PartitionNumber
            dataPartition = CByte((CInt(indexPartition) + 1) Mod 2)
        End If

        layout.Label = volumeLabel
        layout.BlockSize = volumeLabel.blocksize
        layout.IndexPartition = indexPartition
        layout.DataPartition = dataPartition
        Return layout
    End Function

    ''' <summary>把磁带定位到索引数据块的起始 FileMark（与 LTFSWriter 的读取索引流程一致）。</summary>
    Private Shared Sub LocateIndexFileMark(handle As IntPtr, layout As TapeLayout)
        If layout.ExtraPartitionCount = 0 Then
            layout.IndexPartition = 0
            TapeUtils.Locate(handle, 0UL, CByte(0), TapeUtils.LocateDestType.EOD)
            If My.Settings.LTFSWriter_DisablePartition Then
                TapeUtils.Space6(handle, -2, TapeUtils.LocateDestType.FileMark)
            Else
                Dim position As New TapeUtils.PositionData(handle)
                Dim fileMark As ULong = position.FileNumber
                If fileMark <= 1UL Then Throw New IOException("无法定位索引：磁带内容不完整或不是 LTFS 卷。")
                TapeUtils.Locate(handle, CULng(fileMark - 1UL), CByte(0), TapeUtils.LocateDestType.FileMark)
            End If
            TapeUtils.ReadFileMark(handle)
        Else
            TapeUtils.Locate(handle, 3UL, layout.IndexPartition, TapeUtils.LocateDestType.FileMark)
            TapeUtils.ReadFileMark(handle)
        End If
    End Sub

#End Region

#Region "文件提取"

    ''' <summary>把一个索引中被勾选的文件提取到目标文件夹。磁带会自动装载并归位。</summary>
    Friend Function ExtractFiles(slot As TapeSlot, items As List(Of SelectedFile), targetRoot As String,
                                 Optional driveElement As MediumChanger.Element = Nothing,
                                 Optional progress As Action(Of Long) = Nothing,
                                 Optional handleTracking As Action(Of IntPtr) = Nothing) As Integer
        If slot Is Nothing Then Throw New ArgumentNullException(NameOf(slot))
        EnsureChanger()
        If items Is Nothing OrElse items.Count = 0 Then Return 0
        If String.IsNullOrWhiteSpace(targetRoot) Then Throw New ArgumentException("提取目标文件夹为空。", NameOf(targetRoot))
        Directory.CreateDirectory(targetRoot)
        Using mount As MountedTape = MountTape(slot, driveElement)
            If handleTracking IsNot Nothing Then handleTracking(mount.Handle)
            Try
            Dim layout As TapeLayout = ReadTapeLayout(mount.Handle)
            Dim plan As ExtractionPlan = BuildExtractionPlan(items, layout)
            Dim extractedFiles As Integer = 0
            For Each emptyFile In plan.EmptyFiles
                Dim emptyPath As String = BuildTargetPath(targetRoot, emptyFile.RelativePath)
                CreateEmptyFile(emptyPath)
                extractedFiles += 1
            Next
            Dim tempPaths As New Dictionary(Of SelectedFile, String)()
            Dim writtenCounts As New Dictionary(Of SelectedFile, Integer)()
            Dim processed As Integer = 0
            Dim total As Integer = plan.Items.Count
            Dim lastPosition As TapeUtils.PositionData = Nothing
            Try
                For Each planItem In plan.Items
                    processed += 1
                    If processed Mod 64 = 0 OrElse processed = total Then
                        RaiseProgress($"正在提取 {slot.Barcode}：数据块 {processed}/{total}（{planItem.Item.RelativePath}）")
                    End If
                    Dim targetPath As String = BuildTargetPath(targetRoot, planItem.Item.RelativePath)
                    Dim tempPath As String = Nothing
                    If Not tempPaths.TryGetValue(planItem.Item, tempPath) Then
                        tempPath = targetPath & ".part"
                        EnsureParentDirectory(tempPath)
                        If File.Exists(tempPath) Then File.Delete(tempPath)
                        tempPaths(planItem.Item) = tempPath
                    End If
                    ReadExtentToFile(mount.Handle, layout, planItem, tempPath, lastPosition, progress)
                    Dim written As Integer = 0
                    writtenCounts.TryGetValue(planItem.Item, written)
                    written += 1
                    writtenCounts(planItem.Item) = written
                    If written >= planItem.FileExtentTotal Then
                        EnsureParentDirectory(targetPath)
                        If File.Exists(targetPath) Then File.Delete(targetPath)
                        File.Move(tempPath, targetPath)
                        tempPaths.Remove(planItem.Item)
                        extractedFiles += 1
                    End If
                Next
            Catch
                For Each tempPath In tempPaths.Values
                    Try
                        If File.Exists(tempPath) Then File.Delete(tempPath)
                    Catch ex As Exception
                        LogMessage($"清理未完成文件失败：{ex.Message}")
                    End Try
                Next
                Throw
            End Try
            LogMessage($"[{slot.Barcode}] 提取完成：{extractedFiles} 个文件。")
            Return extractedFiles
            Finally
                If handleTracking IsNot Nothing Then handleTracking(IntPtr.Zero)
            End Try
        End Using
    End Function

    Private Function BuildExtractionPlan(items As List(Of SelectedFile), layout As TapeLayout) As ExtractionPlan
        Dim plan As New ExtractionPlan()
        Dim extentTotals As New Dictionary(Of SelectedFile, Integer)()
        For Each item In items
            If item Is Nothing OrElse item.FileIndex Is Nothing Then Continue For
            Dim extents As List(Of ltfsindex.file.extent) = item.FileIndex.extentinfo
            Dim validCount As Integer = 0
            If extents IsNot Nothing Then
                For Each extent In extents
                    If extent IsNot Nothing AndAlso extent.bytecount > 0 Then validCount += 1
                Next
            End If
            If validCount = 0 Then
                plan.EmptyFiles.Add(item)
                Continue For
            End If
            extentTotals(item) = validCount
            Dim extentIndex As Integer = 0
            For Each extent In extents
                If extent Is Nothing OrElse extent.bytecount <= 0 Then Continue For
                Dim planItem As New ExtractionPlanItem With {
                    .Item = item,
                    .Extent = extent,
                    .ExtentIndex = extentIndex,
                    .PartitionNumber = GetPhysicalPartition(extent.partition, layout),
                    .FileExtentTotal = validCount
                }
                plan.Items.Add(planItem)
                extentIndex += 1
            Next
        Next
        plan.Items.Sort(New ExtractionPlanComparer())
        Return plan
    End Function

    Private Shared Function GetPhysicalPartition(partition As ltfsindex.PartitionLabel, layout As TapeLayout) As Byte
        If layout Is Nothing OrElse layout.Label Is Nothing Then Return CByte(partition)
        If CByte(partition) = CByte(layout.Label.partitions.index) Then Return layout.IndexPartition
        Return layout.DataPartition
    End Function

    ''' <summary>定位到 extent 起始块并把 extent 数据写入临时文件（首块按 byteoffset 截断）。</summary>
    Private Sub ReadExtentToFile(handle As IntPtr, layout As TapeLayout, planItem As ExtractionPlanItem, tempPath As String, ByRef lastPosition As TapeUtils.PositionData, Optional progress As Action(Of Long) = Nothing)
        Dim extent As ltfsindex.file.extent = planItem.Extent
        Dim partition As Byte = planItem.PartitionNumber
        Dim startBlock As ULong = CULng(extent.startblock)
        If lastPosition Is Nothing OrElse lastPosition.PartitionNumber <> partition OrElse lastPosition.BlockNumber <> startBlock Then
            TapeUtils.Locate(handle, startBlock, partition, TapeUtils.LocateDestType.Block)
            lastPosition = New TapeUtils.PositionData(handle)
        End If
        Dim fileMode As FileMode = If(File.Exists(tempPath), FileMode.Open, FileMode.Create)
        Dim byteOffset As Long = extent.byteoffset
        Dim remaining As Long = extent.bytecount
        Dim sense As Byte() = Nothing
        Using output As New FileStream(tempPath, fileMode, FileAccess.Write, FileShare.None, 1048576, FileOptions.RandomAccess)
            output.Seek(extent.fileoffset, SeekOrigin.Begin)
            While remaining > 0L
                Dim requested As UInteger = CUInt(Math.Min(CLng(layout.BlockSize), remaining + byteOffset))
                sense = Nothing
                Dim data As Byte() = TapeUtils.ReadBlock(handle, sense, requested, True)
                CheckReadSense(sense)
                If data Is Nothing OrElse data.Length = 0 Then
                    Throw New EndOfStreamException($"读取磁带数据失败（{planItem.Item.RelativePath}）。")
                End If
                If CLng(data.Length) < CLng(requested) Then
                    LogMessage($"警告：数据块短读，期望 {requested} 字节，实际 {data.Length} 字节（{planItem.Item.RelativePath}）。")
                End If
                Dim available As Long = CLng(data.Length) - byteOffset
                If available <= 0L Then
                    Throw New EndOfStreamException($"读取磁带数据失败：数据块长度不足（{planItem.Item.RelativePath}）。")
                End If
                Dim writeLength As Long = Math.Min(remaining, available)
                output.Write(data, CInt(byteOffset), CInt(writeLength))
                If progress IsNot Nothing Then progress(writeLength)
                remaining -= writeLength
                byteOffset = 0L
            End While
        End Using
        lastPosition = New TapeUtils.PositionData(handle)
    End Sub

    Private Shared Sub CheckReadSense(sense As Byte())
        If sense Is Nothing OrElse sense.Length < 3 Then Return
        Dim senseKey As Integer = CInt(sense(2)) And &HF
        If senseKey = 0 Then Return
        Dim eom As Boolean = (CInt(sense(2)) And &H40) <> 0
        If eom AndAlso senseKey = 13 Then Return
        Throw New IOException($"读取磁带数据失败：{TapeUtils.ParseSenseData(sense)}")
    End Sub

    Private Shared Function BuildTargetPath(root As String, relativePath As String) As String
        Return Path.Combine(root, SanitizeRelativePath(relativePath))
    End Function

    Private Shared Function SanitizeRelativePath(relativePath As String) As String
        If String.IsNullOrWhiteSpace(relativePath) Then Return "_"
        Dim normalized As String = relativePath.Replace("/"c, "\"c).TrimStart("\"c)
        Dim parts As String() = normalized.Split(New Char() {"\"c}, StringSplitOptions.RemoveEmptyEntries)
        Dim safeParts As New List(Of String)
        For Each part In parts
            If part = "." OrElse part = ".." Then Continue For
            safeParts.Add(SafeFileName(part))
        Next
        If safeParts.Count = 0 Then Return "_"
        Return String.Join("\", safeParts.ToArray())
    End Function

    Private Shared Function SafeFileName(name As String) As String
        If String.IsNullOrEmpty(name) Then Return "_"
        Dim invalidChars As Char() = Path.GetInvalidFileNameChars()
        Dim builder As New StringBuilder(name.Length)
        For Each c As Char In name
            If Array.IndexOf(invalidChars, c) >= 0 Then
                builder.Append("_"c)
            Else
                builder.Append(c)
            End If
        Next
        Dim result As String = builder.ToString().TrimEnd(" "c, "."c)
        If result.Length = 0 Then Return "_"
        Return result
    End Function

    Private Shared Function SafeDirectoryName(name As String) As String
        Return SafeFileName(If(name, String.Empty))
    End Function

    Private Shared Sub EnsureParentDirectory(filePath As String)
        Dim parent As String = Path.GetDirectoryName(filePath)
        If Not String.IsNullOrWhiteSpace(parent) Then Directory.CreateDirectory(parent)
    End Sub

    Private Shared Sub CreateEmptyFile(filePath As String)
        EnsureParentDirectory(filePath)
        Using stream As New FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
        End Using
    End Sub

    ''' <summary>收集索引中所有被勾选的文件（含递归子目录），返回相对路径。</summary>
    Friend Function CollectSelectedFiles(index As ltfsindex, barcode As String) As List(Of SelectedFile)
        Dim result As New List(Of SelectedFile)
        If index Is Nothing Then Return result
        Dim rootFiles As List(Of ltfsindex.file) = index._file
        If rootFiles IsNot Nothing Then
            For Each childFile In rootFiles
                If childFile Is Nothing OrElse Not childFile.Selected Then Continue For
                If IsSymlink(childFile) Then Continue For
                result.Add(New SelectedFile With {
                    .RelativePath = If(childFile.name, String.Empty),
                    .FileIndex = childFile,
                    .Barcode = barcode
                })
            Next
        End If
        Dim rootDirectories As List(Of ltfsindex.directory) = index._directory
        If rootDirectories IsNot Nothing Then
            For Each directory In rootDirectories
                CollectSelectedFilesInDirectory(directory, barcode, String.Empty, result)
            Next
        End If
        Return result
    End Function

    Private Shared Sub CollectSelectedFilesInDirectory(directory As ltfsindex.directory, barcode As String, relativePath As String, result As List(Of SelectedFile))
        If directory Is Nothing OrElse Not directory.Selected Then Return
        For Each childFile In directory.EnumerateLazyFiles()
            If childFile Is Nothing OrElse Not childFile.Selected Then Continue For
            If IsSymlink(childFile) Then Continue For
            result.Add(New SelectedFile With {
                .RelativePath = JoinRelativePath(relativePath, childFile.name),
                .FileIndex = childFile,
                .Barcode = barcode
            })
        Next
        For Each childDirectory In directory.EnumerateLazyDirectories()
            If childDirectory Is Nothing OrElse Not childDirectory.Selected Then Continue For
            CollectSelectedFilesInDirectory(childDirectory, barcode, JoinRelativePath(relativePath, childDirectory.name), result)
        Next
    End Sub

    Private Shared Function IsSymlink(childFile As ltfsindex.file) As Boolean
        Dim link As String = childFile.symlink
        Return Not String.IsNullOrEmpty(link)
    End Function

    Private Shared Function JoinRelativePath(parentPath As String, childName As String) As String
        Dim parent As String = If(parentPath, String.Empty).TrimEnd("\"c)
        Dim child As String = If(childName, String.Empty).Trim("\"c)
        If parent.Length = 0 Then Return child
        If child.Length = 0 Then Return parent
        Return parent & "\" & child
    End Function

#End Region

#Region "磁带装载与驱动器映射"

    ''' <summary>把磁带从槽位移到空闲驱动器并打开句柄；调用方用 Using 保证磁带归位。</summary>
    Private Function MountTape(slot As TapeSlot) As MountedTape
        Return MountTape(slot, Nothing)
    End Function

    ''' <summary>把磁带移到指定驱动器（Nothing 表示任选空闲驱动器）并打开句柄；调用方用 Using 保证磁带归位。</summary>
    Private Function MountTape(slot As TapeSlot, driveElement As MediumChanger.Element) As MountedTape
        EnsureChanger()
        If slot Is Nothing Then Throw New ArgumentNullException(NameOf(slot))
        If driveElement Is Nothing Then driveElement = FindAvailableDriveElement()
        If driveElement Is Nothing Then
            Throw New InvalidOperationException("带库中没有可用的空闲磁带驱动器（或无法映射到本机磁带设备）。")
        End If
        Dim device As BlockDevice = Nothing
        If Not TryMapDriveElement(driveElement, device) Then
            Throw New InvalidOperationException($"无法把驱动器元素 0x{driveElement.ElementAddress.ToString("X4")} 映射到本机磁带设备。")
        End If
        Dim sense(63) As Byte
        MediumChanger.MoveMedium(_changerPath, CUInt(slot.SlotAddress), CUInt(driveElement.ElementAddress), sense, slot.Element.LUN)
        If GetSenseKey(sense) <> 0 Then
            Throw New IOException($"装载磁带失败（{slot.Barcode}）：{TapeUtils.ParseSenseData(sense)}")
        End If
        Dim mount As New MountedTape(_changerPath, slot, driveElement, _log)
        mount.MarkTapeMoved()
        Try
            If TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream AndAlso Not File.Exists(device.DevicePath) Then
                File.Create(device.DevicePath).Close()
            End If
            Dim handle As IntPtr = IntPtr.Zero
            If Not TapeUtils.OpenTapeDrive(device.DevicePath, handle) Then
                Throw New IOException($"无法打开磁带驱动器 {device.DevicePath}。")
            End If
            mount.Handle = handle
            WaitTapeReady(handle)
            Dim limits As TapeUtils.BlockLimits = TapeUtils.ReadBlockLimits(handle)
            TapeUtils.GlobalBlockLimit = CInt(limits.MaximumBlockLength)
            ApplyEncryptionKey(handle)
            Return mount
        Catch
            mount.Dispose()
            Throw
        End Try
    End Function

    Private ReadOnly TapeReadyTimeoutSeconds As Integer = 240

    ''' <summary>等待驱动器完成介质装载并进入就绪状态。带库自动装载一盘 LTO 磁带通常需要几十秒到数分钟，
    ''' 不能只等几秒，否则会立刻收到 NOT READY / SI_ERR_LOADING_MEDIA。</summary>
    Private Sub WaitTapeReady(handle As IntPtr)
        Dim started As DateTime = DateTime.UtcNow
        Dim lastSense As Byte() = Nothing
        Dim lastLoggedSecond As Integer = -15
        While (DateTime.UtcNow - started).TotalSeconds < TapeReadyTimeoutSeconds
            lastSense = TapeUtils.TestUnitReady(handle)
            If lastSense IsNot Nothing AndAlso lastSense.Length >= 3 AndAlso lastSense(2) = 0 Then Return
            Dim elapsed As Integer = CInt((DateTime.UtcNow - started).TotalSeconds)
            If elapsed - lastLoggedSecond >= 15 Then
                lastLoggedSecond = elapsed
                LogMessage($"等待磁带驱动器就绪（已等待 {elapsed} 秒）：{TapeUtils.ParseSenseData(lastSense)}")
            End If
            Thread.Sleep(500)
        End While
        Dim senseText As String = If(lastSense Is Nothing OrElse lastSense.Length = 0, "未知错误", TapeUtils.ParseSenseData(lastSense))
        Throw New IOException($"磁带驱动器未就绪（等待 {TapeReadyTimeoutSeconds} 秒超时）：{senseText}")
    End Sub

    Private Sub ApplyEncryptionKey(handle As IntPtr)
        Try
            Dim keyFile As String = My.Settings.encKeyFile
            If String.IsNullOrWhiteSpace(keyFile) OrElse Not File.Exists(keyFile) Then Return
            Dim text As String = File.ReadAllText(keyFile).Trim()
            If text.Length = 0 Then Return
            Dim key As Byte() = IOManager.HexStringToByteArray(text)
            If key Is Nothing OrElse key.Length <> 32 Then Return
            Dim sum As Integer = 0
            For index As Integer = 0 To key.Length - 1
                sum += key(index)
            Next
            If sum = 0 Then Return
            TapeUtils.SetEncryption(handle, key)
        Catch ex As Exception
            LogMessage($"设置磁带加密密钥失败：{ex.Message}")
        End Try
    End Sub

    Private Shared Function GetSenseKey(sense As Byte()) As Integer
        If sense Is Nothing OrElse sense.Length < 3 Then Return 0
        Return CInt(sense(2)) And &HF
    End Function

#End Region

#Region "日志与辅助"

    Private Sub EnsureChanger()
        If _changer Is Nothing Then Throw New InvalidOperationException("未提供带库，无法执行磁带操作。")
    End Sub

    Private Sub LogMessage(message As String)
        If _log IsNot Nothing Then _log(message)
    End Sub

    Private Sub RaiseProgress(message As String)
        If _progress IsNot Nothing Then _progress(message)
    End Sub

#End Region

#Region "提取监控辅助"

    ''' <summary>通过 WERL 日志页 0x88 读取磁头错误率（log10 形式，0 表示无错误）。
    ''' 读取使用与提取相同的 SCSI 操作锁，并用短超时 TryEnter，避免拖慢提取。</summary>
    Friend NotInheritable Class HeadErrorRateReader
        Private ReadOnly _lastC1Err(63) As Integer
        Private ReadOnly _lastNoCCPs(63) As Integer

        Friend Function TryRead(handle As IntPtr, ByRef logRate As Double) As Boolean
            logRate = 0
            If handle = IntPtr.Zero OrElse handle = New IntPtr(-1) Then Return False
            Dim handleLock As Object = TapeUtils.GetSCSIOperationLock(handle)
            If Not Threading.Monitor.TryEnter(handleLock, 30) Then Return False
            Try
                Dim header As Byte() = TapeUtils.SCSIReadParam(handle, New Byte() {&H1C, &H1, &H88, &H0, &H4, &H0}, 4, Nothing, 1)
                If header Is Nothing OrElse header.Length <> 4 Then Return False
                Dim pageLen As Integer = (CInt(header(2)) << 8) Or CInt(header(3))
                If pageLen = 0 Then Return False
                pageLen += 4
                Dim page As Byte() = TapeUtils.SCSIReadParam(handle, New Byte() {&H1C, &H1, &H88, CByte((pageLen >> 8) And &HFF), CByte(pageLen And &HFF), &H0}, pageLen, Nothing, 1)
                If page Is Nothing OrElse page.Length <= 4 Then Return False
                Dim tokens As String() = Text.Encoding.ASCII.GetString(page, 4, page.Length - 4).Split(New Char() {ChrW(13), ChrW(10), ChrW(9)}, StringSplitOptions.RemoveEmptyEntries)
                Dim result As Double = Double.NegativeInfinity
                Dim dataValid As Boolean = False
                Dim hasNullChannel As Boolean = False
                For ch As Integer = 4 To tokens.Length - 5 Step 5
                    Dim chan As Integer = (ch - 4) \ 5
                    If chan < 0 OrElse chan >= _lastC1Err.Length Then Continue For
                    Dim c1Err As Integer = 0
                    Dim noCCPs As Integer = 0
                    If Not Integer.TryParse(tokens(ch), Globalization.NumberStyles.HexNumber, Nothing, c1Err) Then Continue For
                    If Not Integer.TryParse(tokens(ch + 4), Globalization.NumberStyles.HexNumber, Nothing, noCCPs) Then Continue For
                    Dim deltaCCPs As Long = CLng(noCCPs) - CLng(_lastNoCCPs(chan))
                    If deltaCCPs > 0L Then
                        dataValid = True
                        Dim deltaErrs As Long = CLng(c1Err) - CLng(_lastC1Err(chan))
                        If deltaErrs >= 0L AndAlso deltaErrs < deltaCCPs Then
                            Dim rateLog As Double = Math.Log10(CDbl(deltaErrs) / CDbl(deltaCCPs) / 2.0 / 1920.0)
                            If rateLog < 0 Then result = Math.Max(result, rateLog)
                        End If
                    Else
                        hasNullChannel = True
                    End If
                    _lastC1Err(chan) = c1Err
                    _lastNoCCPs(chan) = noCCPs
                Next
                If Not dataValid Then Return False
                If hasNullChannel Then result = Math.Max(result, -2.98)
                If Double.IsNegativeInfinity(result) Then result = 0
                If result < -10 Then result = 0
                logRate = result
                Return True
            Catch ex As Exception
                Return False
            Finally
                Threading.Monitor.Exit(handleLock)
            End Try
        End Function
    End Class

#End Region

End Class
