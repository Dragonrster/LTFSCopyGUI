Imports System.IO
Imports System.Text

''' <summary>
''' 解析 LTFSCopyGUI 保存的 CM 报告文本（GetReport 输出，.cm 文件），
''' 并从报告中的“CM RAW DATA”十六进制数据（或直接的原始 CM 二进制文件）恢复完整 CM 数据。
''' 只处理文件，不访问磁带设备。
''' </summary>
Public Class CmReport

    Public Const NotAvailableText As String = "不可用"

    Public Property SourcePath As String = ""
    Public Property ReportText As String = ""

    ' 应用信息
    Public Property Barcode As String = ""
    Public Property Application As String = ""

    ' 使用情况（数据带）
    Public Property LoadCount As String = ""
    Public Property TotalWrite As String = ""
    Public Property TotalRead As String = ""
    Public Property FullVolumeEquivalents As String = ""
    Public Property WriteRetries As String = ""
    Public Property ReadRetries As String = ""
    Public Property UnrecoveredWrites As String = ""
    Public Property UnrecoveredReads As String = ""
    Public Property SuspendedWrites As String = ""
    Public Property SuspendedAppendWrites As String = ""
    Public Property FatalSuspendedWrites As String = ""

    ' 使用情况（清洗带 CU）
    Public Property IsCleanCartridge As Boolean = False
    Public Property CleansPerformed As String = ""
    Public Property CleansRemain As String = ""
    Public Property UsedLength As String = ""

    ' 磁带信息
    Public Property MediaFormat As String = ""
    Public Property SerialNumber As String = ""
    Public Property TapeVendor As String = ""
    Public Property TapeMfgDate As String = ""
    Public Property MediaVendor As String = ""
    Public Property MediaMfgDate As String = ""
    Public Property ParticleType As String = ""

    ' 磁带数据
    Public Property PartitionCount As String = ""
    Public Property EstimatedCapacityLoss As String = ""
    Public ReadOnly Property Partitions As New List(Of String)()

    ' 完整 CM 数据（从原始 8160 字节 buffer 解析；无法恢复时为 Nothing）
    Public Property ParsedCM As TapeUtils.CMParser

    ''' <summary>是否解析到了完整 CM 二进制数据。</summary>
    Public ReadOnly Property HasFullData As Boolean
        Get
            Return ParsedCM IsNot Nothing AndAlso ParsedCM.g_ValidCM
        End Get
    End Property

    ''' <summary>读取并解析 CM 文件：优先按报告文本解析，无原始数据时按原始二进制 CM dump 解析。</summary>
    Public Shared Function ParseFile(path As String) As CmReport
        If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("文件路径为空。", NameOf(path))
        Dim bytes As Byte() = File.ReadAllBytes(path)
        If bytes.Length = 0 Then Throw New FormatException("文件为空。")

        Dim text As String = TryReadReportText(bytes)
        If text IsNot Nothing Then
            If text.IndexOf("APPLICATION INFO", StringComparison.OrdinalIgnoreCase) < 0 Then
                Throw New FormatException("文件内容不是 CM 报告文本（缺少 APPLICATION INFO 段落）。")
            End If
            Dim report As New CmReport With {.SourcePath = path, .ReportText = text}
            report.FillFromReportText(text)
            Dim buffer As Byte() = ExtractCmBuffer(text)
            If buffer IsNot Nothing Then report.TryParseBuffer(buffer)
            Return report
        End If

        ' 原始二进制 CM dump
        Dim rawReport As New CmReport With {.SourcePath = path}
        rawReport.TryParseBuffer(bytes)
        If rawReport.ParsedCM Is Nothing Then
            Throw New FormatException("文件既不是 CM 报告文本，也不是有效的 CM 二进制数据（CM 校验失败）。")
        End If
        rawReport.ReportText = rawReport.ParsedCM.GetReport()
        rawReport.FillFromReportText(rawReport.ReportText)
        Return rawReport
    End Function

    ''' <summary>解析 CM 报告文本（含 RAW DATA 时同时恢复完整 CM 数据）。</summary>
    Public Shared Function ParseText(text As String) As CmReport
        If String.IsNullOrWhiteSpace(text) Then Throw New FormatException("文件内容为空。")
        If text.IndexOf("APPLICATION INFO", StringComparison.OrdinalIgnoreCase) < 0 Then
            Throw New FormatException("文件内容不是 CM 报告文本（缺少 APPLICATION INFO 段落）。")
        End If
        Dim report As New CmReport With {.ReportText = text}
        report.FillFromReportText(text)
        Dim buffer As Byte() = ExtractCmBuffer(text)
        If buffer IsNot Nothing Then report.TryParseBuffer(buffer)
        Return report
    End Function

    ''' <summary>读取文本文件内容；若在探测区内发现 0 字节（二进制）则返回 Nothing。</summary>
    Private Shared Function TryReadReportText(bytes As Byte()) As String
        Dim offset As Integer = 0
        If bytes.Length >= 2 AndAlso bytes(0) = &HFF AndAlso bytes(1) = &HFE Then
            Return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
        End If
        If bytes.Length >= 2 AndAlso bytes(0) = &HFE AndAlso bytes(1) = &HFF Then
            Return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)
        End If
        If bytes.Length >= 3 AndAlso bytes(0) = &HEF AndAlso bytes(1) = &HBB AndAlso bytes(2) = &HBF Then offset = 3

        Dim probeLength As Integer = Math.Min(bytes.Length, 4096)
        For i As Integer = offset To probeLength - 1
            If bytes(i) = 0 Then Return Nothing
        Next

        Try
            Return New UTF8Encoding(False, True).GetString(bytes, offset, bytes.Length - offset)
        Catch ex As DecoderFallbackException
            Return Encoding.Default.GetString(bytes, offset, bytes.Length - offset)
        End Try
    End Function

    ''' <summary>把报告文本中的字段填入当前对象。</summary>
    Public Sub FillFromReportText(text As String)
        Dim partitionSizes As New SortedDictionary(Of Integer, String)()
        Dim section As String = ""

        For Each rawLine As String In text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(New String() {vbLf}, StringSplitOptions.None)
            Dim line As String = rawLine
            If line.StartsWith("+===", StringComparison.Ordinal) Then
                section = GetSectionName(line)
                Continue For
            End If
            If Not line.StartsWith("|", StringComparison.Ordinal) Then Continue For
            If line.Length <= 28 Then Continue For

            Dim label As String = line.Substring(1, 27).Trim()
            If label.Length = 0 OrElse Not label.EndsWith(":", StringComparison.Ordinal) Then Continue For
            label = label.Substring(0, label.Length - 1)

            Dim value As String = line.Substring(28).Trim()
            If value.EndsWith("|", StringComparison.Ordinal) Then value = value.Substring(0, value.Length - 1).TrimEnd()

            Select Case section
                Case "APPLICATION"
                    Select Case label
                        Case "Barcode" : Barcode = value
                        Case "Application" : Application = value
                    End Select
                Case "USAGE"
                    Select Case label
                        Case "Load count" : LoadCount = value
                        Case "Total write" : TotalWrite = value
                        Case "Total read" : TotalRead = value
                        Case "Full volume equivalents" : FullVolumeEquivalents = value
                        Case "Write retries" : WriteRetries = value
                        Case "Read retries" : ReadRetries = value
                        Case "Unrecovered writes" : UnrecoveredWrites = value
                        Case "Unrecovered reads" : UnrecoveredReads = value
                        Case "Suspended writes" : SuspendedWrites = value
                        Case "Suspended append writes" : SuspendedAppendWrites = value
                        Case "Fatal suspended writes" : FatalSuspendedWrites = value
                        Case "Cleans performed" : IsCleanCartridge = True : CleansPerformed = value
                        Case "Cleans remain" : IsCleanCartridge = True : CleansRemain = value
                        Case "Used length" : IsCleanCartridge = True : UsedLength = value
                    End Select
                Case "IDENTITY"
                    Select Case label
                        Case "Format" : MediaFormat = value
                        Case "Serial number" : SerialNumber = value
                        Case "Tape Vendor" : TapeVendor = value
                        Case "Tape mfg date" : TapeMfgDate = value
                        Case "Media Vendor" : MediaVendor = value
                        Case "Media mfg date" : MediaMfgDate = value
                        Case "Particle type" : ParticleType = value
                    End Select
                Case "DATA"
                    Select Case label
                        Case "Total partitions" : PartitionCount = value
                        Case "Estimated capacity loss" : EstimatedCapacityLoss = value
                        Case Else
                            If label.StartsWith("Partition ", StringComparison.Ordinal) AndAlso label.EndsWith(" size", StringComparison.Ordinal) Then
                                Dim numberText As String = label.Substring(10, label.Length - 15)
                                Dim index As Integer
                                If Integer.TryParse(numberText, index) Then partitionSizes(index) = value
                            End If
                    End Select
            End Select
        Next

        For Each pair As KeyValuePair(Of Integer, String) In partitionSizes
            Partitions.Add(pair.Value)
        Next
    End Sub

    ''' <summary>把报告里的值转成界面显示文本；缺失或 Not available 显示“不可用”。</summary>
    Public Shared Function ToDisplayValue(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return NotAvailableText
        Dim trimmed As String = value.Trim()
        If trimmed.Length = 0 Then Return NotAvailableText
        If String.Equals(trimmed, "Not available", StringComparison.OrdinalIgnoreCase) Then Return NotAvailableText
        If String.Equals(trimmed, "Unknown", StringComparison.OrdinalIgnoreCase) Then Return NotAvailableText
        Return trimmed
    End Function

    ''' <summary>从报告文本的 CM RAW DATA 十六进制区恢复 8160 字节 CM buffer。</summary>
    Public Shared Function ExtractCmBuffer(reportText As String) As Byte()
        If String.IsNullOrEmpty(reportText) Then Return Nothing
        If reportText.IndexOf("CM RAW DATA", StringComparison.OrdinalIgnoreCase) < 0 Then Return Nothing

        Dim data As New List(Of Byte)()
        For Each line As String In reportText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(New String() {vbLf}, StringSplitOptions.None)
            If Not line.StartsWith("|", StringComparison.Ordinal) Then Continue For
            If Not System.Text.RegularExpressions.Regex.IsMatch(line, "^\|\s*[0-9A-Fa-f]{1,5}h: ") Then Continue For
            Dim marker As Integer = line.IndexOf("h: ", StringComparison.Ordinal)
            Dim hexPart As String = line.Substring(marker + 3)
            Dim count As Integer = Math.Min(48, hexPart.Length)
            Dim offset As Integer = 0
            While offset + 2 <= count
                Dim pair As String = hexPart.Substring(offset, 2)
                If pair(0) = " "c OrElse pair(1) = " "c Then Exit While
                Dim b As Byte
                If Not Byte.TryParse(pair, Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, b) Then Exit While
                data.Add(b)
                offset += 3
            End While
        Next

        If data.Count < &H1000 Then Return Nothing
        Return data.ToArray()
    End Function

    Private Sub TryParseBuffer(buffer As Byte())
        If buffer Is Nothing OrElse buffer.Length < &H1000 Then Return
        Try
            Dim errorMsg As Exception = Nothing
            Dim cm As New TapeUtils.CMParser(buffer, errorMsg)
            If cm.g_ValidCM Then ParsedCM = cm
        Catch
        End Try
    End Sub

    Private Shared Function GetSectionName(headerLine As String) As String
        If headerLine.IndexOf("APPLICATION INFO", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "APPLICATION"
        If headerLine.IndexOf("MEDIUM USAGE", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "USAGE"
        If headerLine.IndexOf("MEDIUM IDENTITY", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "IDENTITY"
        If headerLine.IndexOf("DATA ON TAPE", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "DATA"
        If headerLine.IndexOf("WRAP ANALYSIS", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "WRAP"
        If headerLine.IndexOf("CM RAW DATA", StringComparison.OrdinalIgnoreCase) >= 0 Then Return "RAW"
        Return ""
    End Function

End Class
