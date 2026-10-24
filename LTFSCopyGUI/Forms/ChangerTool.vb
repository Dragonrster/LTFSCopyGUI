Imports System
Imports System.ComponentModel
Imports System.ComponentModel.DataAnnotations
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports LTFSCopyGUI.TapeUtils
Imports NAudio.MediaFoundation
Imports Serilog
Imports Serilog.Context

Public Class ChangerTool
    Private ReadOnly _logSessionId As String = $"changer-{Guid.NewGuid().ToString("N").Substring(0, 8)}"
    Public LoadComplete As Boolean = False
    '<TypeConverter(GetType(ExpandableObjectConverter))>
    <TypeConverter(GetType(ListTypeDescriptor(Of List(Of MediumChanger), MediumChanger)))>
    Public Property LastDeviceList As List(Of MediumChanger)
    Public ReadOnly Property CurrentChanger As MediumChanger
        Get
            If LastDeviceList Is Nothing Then Return Nothing
            If SelectedIndex < 0 Or SelectedIndex > LastDeviceList.Count - 1 Then Return Nothing
            Return LastDeviceList(SelectedIndex)
        End Get
    End Property
    Private _SelectedIndex As Integer
    Public Property SelectedIndex As Integer
        Set(value As Integer)
            If _SelectedIndex = value Then Exit Property
            _SelectedIndex = Math.Max(0, value)
            If Not LoadComplete Then Exit Property
            If CheckBox1.Checked Then
                SetUILock(True)
                Task.Run(Sub()
                             RefreshCurrentChanger()
                             Invoke(Sub()
                                        SwitchChanger()
                                        SetUILock(False)
                                    End Sub)
                         End Sub)
            Else
                SwitchChanger()
            End If
        End Set
        Get
            Return _SelectedIndex
        End Get
    End Property
    Private Sub ChangerTool_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Lifecycle")
                        Log.Information("Changer window loading.")
                    End Using
                End Using
            End Using
        End Using
        Text = $"ChangerTool - {ApplicationWheels.ApplicationInfo}"
        RefreshMCList()
        SetUILock(True)
        Task.Run(Sub()
                     RefreshCurrentChanger()
                     Invoke(Sub()
                                SwitchChanger()
                                SetUILock(False)
                            End Sub)
                 End Sub)
        LoadComplete = True
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Lifecycle")
                        Log.Information("Changer window loaded. DeviceCount={DeviceCount} SelectedIndex={SelectedIndex}.", If(LastDeviceList Is Nothing, 0, LastDeviceList.Count), SelectedIndex)
                    End Using
                End Using
            End Using
        End Using
    End Sub
    Public Sub RefreshMCList()
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "DeviceRefresh")
                        Log.Information("Medium changer enumeration started.")
                    End Using
                End Using
            End Using
        End Using
        Dim DeviceList As List(Of MediumChanger)
        Try
            DeviceList = GetMediumChangerList()
        Catch ex As Exception
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Error")
                            Log.Error(ex, "Medium changer enumeration failed.")
                        End Using
                    End Using
                End Using
            End Using
            Throw
        End Try
        LoadComplete = False
        ListBox1.Items.Clear()
        Dim DevList As List(Of MediumChanger)
        DevList = DeviceList
        LastDeviceList = DeviceList
        For Each D As MediumChanger In DevList
            ListBox1.Items.Add(D.ToString())
        Next
        ListBox1.SelectedIndex = Math.Min(SelectedIndex, ListBox1.Items.Count - 1)
        LoadComplete = True
        SelectedIndex = ListBox1.SelectedIndex
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "DeviceRefresh")
                        Log.Information("Medium changer enumeration completed. DeviceCount={DeviceCount} SelectedIndex={SelectedIndex}.", DeviceList.Count, SelectedIndex)
                    End Using
                End Using
            End Using
        End Using
    End Sub
    Public FullElement, EmptyElement As List(Of MediumChanger.Element)
    Public Sub RefreshCurrentChanger()
        If CurrentChanger Is Nothing Then
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "DeviceRefresh")
                            Log.Warning("Current medium changer refresh was skipped because no changer is selected.")
                        End Using
                    End Using
                End Using
            End Using
            Exit Sub
        End If
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "DeviceRefresh")
                        Log.Information("Current medium changer refresh started. DeviceIndex={DeviceIndex}.", CurrentChanger.DevIndex)
                    End Using
                End Using
            End Using
        End Using
        Try
            CurrentChanger.RefreshElementStatus(Not CheckBox2.Checked)
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "DeviceRefresh")
                            Log.Information("Current medium changer refresh completed. ElementCount={ElementCount}.", If(CurrentChanger.Elements Is Nothing, 0, CurrentChanger.Elements.Count))
                        End Using
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Error")
                            Log.Error(ex, "Current medium changer refresh failed. DeviceIndex={DeviceIndex}.", CurrentChanger.DevIndex)
                        End Using
                    End Using
                End Using
            End Using
            MessageBox.Show(ex.ToString())
        End Try
    End Sub
    Public Sub SwitchChanger()
        If CurrentChanger Is Nothing Then
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Selection")
                            Log.Warning("Changer view switch was skipped because no changer is selected.")
                        End Using
                    End Using
                End Using
            End Using
        End If
        If CurrentChanger Is Nothing OrElse CurrentChanger.Elements Is Nothing OrElse CurrentChanger.Elements.Count = 0 Then RefreshCurrentChanger()
        Try
            TextBox1.Text = CurrentChanger.GetSerializedText()
            FullElement = New List(Of MediumChanger.Element)
            EmptyElement = New List(Of MediumChanger.Element)
            For Each e As MediumChanger.Element In CurrentChanger.Elements
                If e.Full Then FullElement.Add(e) Else EmptyElement.Add(e)
            Next
            ComboBox1.Items.Clear()
            ComboBox2.Items.Clear()
            For Each e As MediumChanger.Element In FullElement
                Dim ctext As String = $"{e.PrimaryVolumeTagInformation} @0x{Hex(e.ElementAddress).PadLeft(4, "0"c)} LUN{e.LUN}"
                Dim itext As String = e.Identifier.Replace("  ", " ").Replace("  ", " ").Replace("  ", " ")
                Dim ttext As String = e.ElementTypeCode.ToString()
                If itext.Length > 0 Then itext &= " "
                itext &= ttext
                itext = itext.TrimEnd(" "c)
                If itext.Length > 0 Then ctext = $"{ctext}({itext})"
                ComboBox1.Items.Add(ctext)
            Next
            For Each e As MediumChanger.Element In EmptyElement
                Dim ctext As String = $"0x{Hex(e.ElementAddress).PadLeft(4, "0"c)} LUN{e.LUN}"
                Dim itext As String = e.Identifier.Replace("  ", " ").Replace("  ", " ").Replace("  ", " ")
                Dim ttext As String = e.ElementTypeCode.ToString()
                If itext.Length > 0 Then itext &= " "
                itext &= ttext
                itext = itext.TrimEnd(" "c)
                If itext.Length > 0 Then ctext = $"{ctext}({itext})"
                ComboBox2.Items.Add(ctext)
            Next
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Selection")
                            Log.Information("Changer view updated. FullElementCount={FullElementCount} EmptyElementCount={EmptyElementCount}.", FullElement.Count, EmptyElement.Count)
                        End Using
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Error")
                            Log.Error(ex, "Changer view update failed.")
                        End Using
                    End Using
                End Using
            End Using
        End Try

    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "DeviceRefresh")
                        Log.Information("Medium changer refresh requested by the user.")
                    End Using
                End Using
            End Using
        End Using
        RefreshMCList()
    End Sub

    Private Sub ListBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ListBox1.SelectedIndexChanged
        If Not LoadComplete Then Exit Sub
        SelectedIndex = ListBox1.SelectedIndex
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        If ComboBox1.SelectedIndex < 0 Then Exit Sub
        If ComboBox2.SelectedIndex < 0 Then Exit Sub
        If FullElement Is Nothing Then Exit Sub
        If EmptyElement Is Nothing Then Exit Sub
        If ComboBox1.SelectedIndex > FullElement.Count - 1 Then Exit Sub
        If ComboBox2.SelectedIndex > EmptyElement.Count - 1 Then Exit Sub
        If MessageBox.Show(New Form With {.TopMost = True}, $"{ComboBox1.SelectedItem} -> {ComboBox2.SelectedItem}", "", MessageBoxButtons.OKCancel) = DialogResult.OK Then
            Dim drv As String = $"\\.\CHANGER{CurrentChanger.DevIndex}"
            If CurrentChanger.device IsNot Nothing Then drv = CurrentChanger.device.DevicePath
            Dim LUN As Byte = FullElement(ComboBox1.SelectedIndex).LUN
            Dim srcElementIndex = ComboBox1.SelectedIndex
            Dim destElementIndex = ComboBox2.SelectedIndex
            Dim srcElement As MediumChanger.Element = FullElement(srcElementIndex)
            Dim destElement As MediumChanger.Element = EmptyElement(destElementIndex)
            Dim src As UInt32 = srcElement.ElementAddress
            Dim dest As UInt32 = destElement.ElementAddress
            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Move")
                            Log.Information("Medium move requested. DevicePath={DevicePath} SourceAddress={SourceAddress} DestinationAddress={DestinationAddress} LUN={LUN}.", drv, src, dest, LUN)
                        End Using
                    End Using
                End Using
            End Using
            srcElement.Full = Not srcElement.Full
            destElement.Full = Not destElement.Full
            destElement.PrimaryVolumeTagInformation = srcElement.PrimaryVolumeTagInformation
            srcElement.PrimaryVolumeTagInformation = ""
            SetUILock(True)
            Dim th As New Thread(
                Sub()
                    Dim sense(63) As Byte
                    Dim succ As Boolean = False
                    Dim ex As Exception = Nothing
                    Try
                        MediumChanger.MoveMedium(drv, src, dest, sense, LUN:=LUN)
                        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Move")
                                        Log.Information("Medium move command completed. SourceAddress={SourceAddress} DestinationAddress={DestinationAddress}.", src, dest)
                                    End Using
                                End Using
                            End Using
                        End Using
                    Catch ex
                        succ = False
                        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Error")
                                        Log.Error(ex, "Medium move command failed. SourceAddress={SourceAddress} DestinationAddress={DestinationAddress}.", src, dest)
                                    End Using
                                End Using
                            End Using
                        End Using
                    Finally
                        succ = True
                    End Try
                    Invoke(Sub()
                               If CheckBox1.Checked Then
                                   Task.Run(
                                   Sub()
                                       RefreshCurrentChanger()
                                       Invoke(Sub()
                                                  SwitchChanger()
                                                  SetUILock(False)
                                              End Sub)
                                   End Sub)
                               Else
                                   Invoke(Sub()
                                              SwitchChanger()
                                              SetUILock(False)
                                          End Sub)
                               End If
                               If succ Then
                                   MessageBox.Show(New Form With {.TopMost = True}, $"Finished{vbCrLf}{ParseSenseData(sense)}")
                               Else
                                   MessageBox.Show(New Form With {.TopMost = True}, $"Error: {ex.ToString}")
                               End If
                           End Sub)
                End Sub)
            th.Start()
        End If

    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "DeviceRefresh")
                        Log.Information("Current medium changer refresh requested by the user.")
                    End Using
                End Using
            End Using
        End Using
        SetUILock(True)
        Task.Run(
            Sub()
                RefreshCurrentChanger()
                Invoke(Sub()
                           SwitchChanger()
                           SetUILock(False)
                       End Sub)
            End Sub)
    End Sub

    Public Sub SetUILock(Lock As Boolean)
        Invoke(Sub()
                   For Each c As Control In Controls
                       c.Enabled = Not Lock
                   Next
               End Sub)
    End Sub

    Private Sub ChangerTool_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        If Not LoadComplete Then Exit Sub
        SuspendLayout()
        Dim sample As New ChangerTool
        ComboBox1.Width = CInt((Width - sample.Width) / 2 + sample.ComboBox1.Width)
        ComboBox2.Width = CInt((Width - sample.Width) / 2 + sample.ComboBox2.Width)
        ComboBox2.Left = CInt((Width - sample.Width) / 2 + sample.ComboBox2.Left)
        Label1.Left = CInt((Width - sample.Width) / 2 + sample.Label1.Left)
        ResumeLayout()
    End Sub

    Private Sub 排序ToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles 排序ToolStripMenuItem.Click
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Sort")
                        Log.Information("Medium changer sorting started.")
                    End Using
                End Using
            End Using
        End Using
        SetUILock(True)
        Task.Run(
            Sub()
                RefreshCurrentChanger()
                Dim drv As String = $"\\.\CHANGER{CurrentChanger.DevIndex}"
                If CurrentChanger.device IsNot Nothing Then drv = CurrentChanger.device.DevicePath
                Invoke(Sub() TextBox1.Clear())
                Dim srcList As New List(Of MediumChanger.Element)
                Dim TLList As New List(Of String)
                For Each el As MediumChanger.Element In CurrentChanger.Elements
                    If el.ElementTypeCode = MediumChanger.Element.ElementTypeCodes.StorageElement Then
                        srcList.Add(el)
                        If el.Full Then TLList.Add(el.PrimaryVolumeTagInformation)
                    End If
                Next
                srcList.Sort(New Comparison(Of MediumChanger.Element)(
                     Function(a As MediumChanger.Element, b As MediumChanger.Element) As Integer
                         Return a.ElementAddress.CompareTo(b.ElementAddress)
                     End Function))
                TLList.Sort()
                Dim IsOnTarget As New Func(Of Boolean)(
                    Function() As Boolean
                        For i As Integer = 0 To TLList.Count - 1
                            If srcList(i).PrimaryVolumeTagInformation <> TLList(i) Then Return False
                        Next
                        Return True
                    End Function)
                While Not IsOnTarget()
                    Dim movecount As Integer = 0
                    For i As Integer = 0 To TLList.Count - 1
                        If Not srcList(i).Full Then
                            Dim j As Integer = 0
                            For j = 0 To srcList.Count - 1
                                If srcList(j).PrimaryVolumeTagInformation = TLList(i) Then
                                    Dim info As String = $"0x{Hex(srcList(j).ElementAddress).PadLeft(4, "0"c)} {srcList(j).PrimaryVolumeTagInformation} -> 0x{Hex(srcList(i).ElementAddress).PadLeft(4, "0"c)}{vbCrLf}"
                                    Invoke(Sub() TextBox1.AppendText(info))
                                    While True
                                        Dim sense(63) As Byte
                                        Try
                                            MediumChanger.MoveMedium(drv, srcList(j).ElementAddress, srcList(i).ElementAddress, sense, LUN:=srcList(j).LUN)
                                            Dim sensekey As Byte = CByte(sense(2) And &HF)
                                            If sensekey <> 0 Then Throw New Exception("SCSI Sense Error")
                                        Catch ex As Exception
                                            Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                                                Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                                                    Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                                                        Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Error")
                                                            Log.Error(ex, "Medium changer sorting move failed. SourceAddress={SourceAddress} DestinationAddress={DestinationAddress}.", srcList(j).ElementAddress, srcList(i).ElementAddress)
                                                        End Using
                                                    End Using
                                                End Using
                                            End Using
                                            Dim result As DialogResult
                                            Invoke(Sub() result = MessageBox.Show(New Form With {.TopMost = True}, $"Error: {ex.ToString}{vbCrLf}{ParseSenseData(sense)}", "", MessageBoxButtons.AbortRetryIgnore))
                                            Select Case result
                                                Case DialogResult.Ignore
                                                    Exit While
                                                Case DialogResult.Cancel
                                                    Invoke(Sub()
                                                               SetUILock(False)
                                                           End Sub)
                                                    Exit Sub
                                                Case DialogResult.Retry
                                                    Continue While
                                            End Select
                                        End Try
                                        Exit While
                                    End While
                                    srcList(i).Full = True
                                    srcList(i).PrimaryVolumeTagInformation = srcList(j).PrimaryVolumeTagInformation
                                    srcList(j).Full = False
                                    srcList(j).PrimaryVolumeTagInformation = ""
                                    movecount += 1
                                    Exit For
                                End If
                            Next
                        End If
                    Next
                    If movecount = 0 Then
                        For i As Integer = TLList.Count To srcList.Count - 1
                            If Not srcList(i).Full Then
                                For j As Integer = 0 To TLList.Count - 1
                                    If srcList(j).Full AndAlso srcList(j).PrimaryVolumeTagInformation <> TLList(j) Then
                                        Dim info As String = $"0x{Hex(srcList(j).ElementAddress).PadLeft(4, "0"c)} {srcList(j).PrimaryVolumeTagInformation} -> 0x{Hex(srcList(i).ElementAddress).PadLeft(4, "0"c)}{vbCrLf}"
                                        Invoke(Sub() TextBox1.AppendText(info))
                                        While True
                                            Dim sense(63) As Byte
                                            Try
                                                MediumChanger.MoveMedium(drv, srcList(j).ElementAddress, srcList(i).ElementAddress, sense, LUN:=srcList(j).LUN)
                                                Dim sensekey As Byte = CByte(sense(2) And &HF)
                                                If sensekey <> 0 Then Throw New Exception("SCSI Sense Error")
                                            Catch ex As Exception
                                                Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                                                    Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                                                        Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                                                            Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Error")
                                                                Log.Error(ex, "Medium changer sorting move failed. SourceAddress={SourceAddress} DestinationAddress={DestinationAddress}.", srcList(j).ElementAddress, srcList(i).ElementAddress)
                                                            End Using
                                                        End Using
                                                    End Using
                                                End Using
                                                Dim result As DialogResult
                                                Invoke(Sub() result = MessageBox.Show(New Form With {.TopMost = True}, $"Error: {ex.ToString}{vbCrLf}{ParseSenseData(sense)}", "", MessageBoxButtons.AbortRetryIgnore))
                                                Select Case result
                                                    Case DialogResult.Ignore
                                                        Exit While
                                                    Case DialogResult.Cancel
                                                        Invoke(Sub()
                                                                   SetUILock(False)
                                                               End Sub)
                                                        Exit Sub
                                                    Case DialogResult.Retry
                                                        Continue While
                                                End Select
                                            End Try
                                            Exit While
                                        End While
                                        srcList(i).Full = True
                                        srcList(i).PrimaryVolumeTagInformation = srcList(j).PrimaryVolumeTagInformation
                                        srcList(j).Full = False
                                        srcList(j).PrimaryVolumeTagInformation = ""
                                        movecount += 1
                                        Exit For
                                    End If
                                Next
                                Exit For
                            End If
                        Next
                    End If
                    If movecount = 0 Then Exit While
                End While
                Invoke(Sub()
                           SetUILock(False)
                       End Sub)
                Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                    Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                        Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                            Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Sort")
                                Log.Information("Medium changer sorting completed.")
                            End Using
                        End Using
                    End Using
                End Using
            End Sub)
    End Sub

    Private Sub ChangerTool_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        Select Case e.KeyCode
            Case Keys.F12
                Dim SP1 As New SettingPanel
                SP1.Text = Text
                SP1.SelectedObject = Me
                SP1.Show()
        End Select
    End Sub

    Private Sub 批量擦除ToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles 批量擦除ToolStripMenuItem.Click
        If Not (MessageBox.Show(My.Resources.ResText_DataLossWarning, My.Resources.ResText_Warning, MessageBoxButtons.OKCancel) = DialogResult.OK) Then Exit Sub
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Erase")
                        Log.Information("Batch medium erase started.")
                    End Using
                End Using
            End Using
        End Using
        SetUILock(True)
        Task.Run(Sub()
                     Invoke(Sub() TextBox1.Text = $"Loading elements{vbCrLf}")
                     RefreshCurrentChanger()
                     If FullElement Is Nothing OrElse EmptyElement Is Nothing Then
                         SetUILock(False)
                         Exit Sub
                     End If
                     Dim changerPath As String = $"\\.\CHANGER{CurrentChanger.DevIndex}"
                     If CurrentChanger.device IsNot Nothing Then changerPath = CurrentChanger.device.DevicePath
                     Dim sense As Byte() = SCSIReadParam(TapeDrive:=changerPath, cdbData:=New Byte() {3, 0, 0, 0, &H12, 0}, paramLen:=18)
                     Dim ToErase As New List(Of MediumChanger.Element)
                     For Each fe As MediumChanger.Element In FullElement
                         If fe.ElementTypeCode = MediumChanger.Element.ElementTypeCodes.StorageElement OrElse fe.ElementTypeCode = MediumChanger.Element.ElementTypeCodes.ImportExportElement Then
                             If fe.Full Then ToErase.Add(fe)
                         End If
                     Next
                     Dim AllDrivers As New List(Of MediumChanger.Element)
                     For Each fe As MediumChanger.Element In EmptyElement
                         If fe.ElementTypeCode = MediumChanger.Element.ElementTypeCodes.DataTransferElement Then
                             fe.Identifier = fe.Identifier.Replace("  ", " ").Replace("  ", " ").Replace("  ", " ")
                             AllDrivers.Add(fe)
                         End If
                     Next
                     Dim drvList As List(Of BlockDevice) = GetTapeDriveList()
                     Dim drvMapping As New Dictionary(Of MediumChanger.Element, BlockDevice)
                     For Each drv As MediumChanger.Element In AllDrivers
                         Dim found As Boolean = False
                         For Each d As BlockDevice In drvList
                             If drv.Identifier.EndsWith(d.SerialNumber) Then
                                 found = True
                                 Invoke(Sub() TextBox1.AppendText($"Find drive [0x{Hex(drv.ElementAddress).PadLeft(4, "0"c)}]{drv.Identifier} <=> {d.SerialNumber} {d.DevicePath}{vbCrLf}"))
                                 drvMapping.Add(drv, d)
                                 Exit For
                             End If
                         Next
                     Next
                     Dim eraseSucceeded As Boolean = False
                     Try
                         If drvMapping.Keys.Count = 0 Then
                             Throw New Exception("No drive found.")
                         End If
                         While ToErase.Count > 0
                             Dim eraseTaskCount As Integer = 0
                             Dim eraseFinEvent As New AutoResetEvent(False)

                             Dim eraseMapping As New Dictionary(Of MediumChanger.Element, MediumChanger.Element) 'Drive <=> Slot
                             For i As Integer = 0 To drvMapping.Keys.Count - 1
                                 If ToErase.Count = 0 Then Exit For

                                 Dim currentDrive As MediumChanger.Element = drvMapping.Keys(i)
                                 Dim currentSlot As MediumChanger.Element = ToErase(0)
                                 Invoke(Sub() TextBox1.AppendText($"Assign drive [0x{Hex(currentDrive.ElementAddress).PadLeft(4, "0"c)}]{currentDrive.Identifier} <=> [0x{Hex(currentSlot.ElementAddress).PadLeft(4, "0"c)}]Element {currentSlot.PrimaryVolumeTagInformation}{vbCrLf}"))
                                 eraseMapping.Add(currentDrive, currentSlot)
                                 ToErase.RemoveAt(0)
                             Next
                             Dim finList As New List(Of MediumChanger.Element)
                             For i As Integer = 0 To eraseMapping.Keys.Count - 1
                                 Dim drv As MediumChanger.Element = eraseMapping.Keys(i)
                                 Dim slot As MediumChanger.Element = eraseMapping(drv)
                                 Invoke(Sub() TextBox1.AppendText($"Move [0x{Hex(slot.ElementAddress).PadLeft(4, "0"c)}]{slot.PrimaryVolumeTagInformation} -> [0x{Hex(drv.ElementAddress).PadLeft(4, "0"c)}]{drv.Identifier}{vbCrLf}"))
                                 ApplicationWheels.TryExecute(Function() As Byte()
                                                                  Dim senseReturn(63) As Byte
                                                                  MediumChanger.MoveMedium(changerPath, slot.ElementAddress, drv.ElementAddress, senseReturn, slot.LUN)
                                                                  Return senseReturn
                                                              End Function)
                                 Interlocked.Increment(eraseTaskCount)
                                 Task.Run(Sub()
                                              Invoke(Sub() TextBox1.AppendText($"Erase {drvMapping(drv).DevicePath}{vbCrLf}"))
                                              ApplicationWheels.TryExecute(Function() As Byte()
                                                                               Dim senseReturn(63) As Byte
                                                                               SCSIReadParam(TapeDrive:=drvMapping(drv).DevicePath, {&HB, 0, 0, &HFF, &HFF, 0}, 0, Function(s As Byte())
                                                                                                                                                                       senseReturn = s
                                                                                                                                                                       Return True
                                                                                                                                                                   End Function)
                                                                               Return senseReturn
                                                                           End Function, 1)
                                              ApplicationWheels.TryExecute(Function() As Byte()
                                                                               Dim senseReturn(63) As Byte
                                                                               SCSIReadParam(TapeDrive:=drvMapping(drv).DevicePath, {4, 0, 1, 0, 0, 0}, 0, Function(s As Byte())
                                                                                                                                                               senseReturn = s
                                                                                                                                                               Return True
                                                                                                                                                           End Function)
                                                                               Return senseReturn
                                                                           End Function, 1)
                                              SyncLock finList
                                                  finList.Add(drv)
                                              End SyncLock
                                              Interlocked.Decrement(eraseTaskCount)
                                              eraseFinEvent.Set()
                                          End Sub)
                             Next
                             While eraseTaskCount > 0
                                 SyncLock finList
                                     For Each drv As MediumChanger.Element In finList
                                         Dim slot As MediumChanger.Element = eraseMapping(drv)
                                         Invoke(Sub() TextBox1.AppendText($"Move [0x{Hex(drv.ElementAddress).PadLeft(4, "0"c)}]{drv.Identifier}_{slot.PrimaryVolumeTagInformation} -> [0x{Hex(slot.ElementAddress).PadLeft(4, "0"c)}]{vbCrLf}"))
                                         MediumChanger.MoveMedium(changerPath, drv.ElementAddress, slot.ElementAddress, sense, drv.LUN)
                                         ApplicationWheels.TryExecute(Function() As Byte()
                                                                          Dim senseReturn(63) As Byte
                                                                          MediumChanger.MoveMedium(changerPath, drv.ElementAddress, slot.ElementAddress, senseReturn, drv.LUN)
                                                                          Return senseReturn
                                                                      End Function)
                                     Next
                                     finList.Clear()
                                 End SyncLock
                                 eraseFinEvent.WaitOne(1000)
                             End While
                             For Each drv As MediumChanger.Element In finList
                                 Dim slot As MediumChanger.Element = eraseMapping(drv)
                                 Invoke(Sub() TextBox1.AppendText($"Move [0x{Hex(drv.ElementAddress).PadLeft(4, "0"c)}]{drv.Identifier}_{slot.PrimaryVolumeTagInformation} -> [0x{Hex(slot.ElementAddress).PadLeft(4, "0"c)}]{vbCrLf}"))
                                 ApplicationWheels.TryExecute(Function() As Byte()
                                                                  Dim senseReturn(63) As Byte
                                                                  MediumChanger.MoveMedium(changerPath, drv.ElementAddress, slot.ElementAddress, senseReturn, drv.LUN)
                                                                  Return senseReturn
                                                              End Function)
                             Next
                         End While
                         Invoke(Sub() TextBox1.AppendText("Finished"))
                         eraseSucceeded = True
                     Catch ex As Exception
                         Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                             Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                                 Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                                     Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Error")
                                         Log.Error(ex, "Batch medium erase failed.")
                                     End Using
                                 End Using
                             End Using
                         End Using
                         MessageBox.Show(ex.ToString())
                     End Try

                     SetUILock(False)
                     Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
                         Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                             Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                                 Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "Erase")
                                     Log.Information("Batch medium erase worker finished. Succeeded={Succeeded}.", eraseSucceeded)
                                 End Using
                             End Using
                         End Using
                     End Using
                 End Sub)

    End Sub

    Private Shared Function GetLocalizedString(key As String) As String
        Dim resourceManager As New ComponentResourceManager(GetType(ChangerTool))
        Dim value As String = resourceManager.GetString(key)
        If String.IsNullOrEmpty(value) Then Return key
        Return value
    End Function

    Private Shared Function GetSafeDirectoryName(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return "_"
        Dim invalidChars As Char() = IO.Path.GetInvalidFileNameChars()
        Dim builder As New StringBuilder()
        For Each c As Char In name.Trim()
            If Array.IndexOf(invalidChars, c) >= 0 Then
                builder.Append("_"c)
            Else
                builder.Append(c)
            End If
        Next
        Return builder.ToString()
    End Function

    Private Shared Function GetLocalCMDirectory(barcode As String) As String
        If String.IsNullOrWhiteSpace(My.Settings.schemaPath) Then Return Nothing
        Return IO.Path.Combine(My.Settings.schemaPath, GetSafeDirectoryName(barcode))
    End Function

    Private Shared Function LocalCMExists(barcode As String) As Boolean
        If String.IsNullOrWhiteSpace(barcode) Then Return False
        Dim directory As String = GetLocalCMDirectory(barcode)
        If directory Is Nothing OrElse Not IO.Directory.Exists(directory) Then Return False
        Return IO.Directory.GetFiles(directory, "*.cm").Length > 0
    End Function

    Private Shared Function SaveLocalCMReport(barcode As String, report As String) As String
        Dim directory As String = GetLocalCMDirectory(barcode)
        If directory Is Nothing Then Return Nothing
        IO.Directory.CreateDirectory(directory)
        Dim path As String = IO.Path.Combine(directory, $"CM_{Now.ToString("yyyyMMdd_HHmmss")}.cm")
        IO.File.WriteAllText(path, report, New UTF8Encoding(False))
        Return path
    End Function

    Private Function GetStoredTapeElements() As List(Of MediumChanger.Element)
        Dim tapes As New List(Of MediumChanger.Element)
        If FullElement Is Nothing Then Return tapes
        For Each element As MediumChanger.Element In FullElement
            If Not element.Full Then Continue For
            If element.ElementTypeCode = MediumChanger.Element.ElementTypeCodes.StorageElement OrElse
               element.ElementTypeCode = MediumChanger.Element.ElementTypeCodes.ImportExportElement Then
                tapes.Add(element)
            End If
        Next
        Return tapes
    End Function

    Private Function GetAvailableTapeDrives() As List(Of MediumChanger.Element)
        Dim drives As New List(Of MediumChanger.Element)
        If EmptyElement Is Nothing Then Return drives
        For Each element As MediumChanger.Element In EmptyElement
            If element.ElementTypeCode = MediumChanger.Element.ElementTypeCodes.DataTransferElement Then
                drives.Add(element)
            End If
        Next
        Return drives
    End Function

    Private Function GetMappedDriveList(drives As List(Of MediumChanger.Element)) As List(Of Tuple(Of MediumChanger.Element, BlockDevice))
        Dim result As New List(Of Tuple(Of MediumChanger.Element, BlockDevice))()
        If drives Is Nothing OrElse drives.Count = 0 Then Return result
        Dim deviceList As List(Of BlockDevice) = GetTapeDriveList()
        Dim usedDevicePaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each driveElement As MediumChanger.Element In drives
            Dim identifier As String = driveElement.Identifier.Replace("  ", " ")
            identifier = identifier.Replace("  ", " ")
            identifier = identifier.Replace("  ", " ")
            For Each device As BlockDevice In deviceList
                If identifier.EndsWith(device.SerialNumber) Then
                    If usedDevicePaths.Add(device.DevicePath) Then
                        result.Add(New Tuple(Of MediumChanger.Element, BlockDevice)(driveElement, device))
                    End If
                    Exit For
                End If
            Next
        Next
        Return result
    End Function

    Private Function SelectBatchDrives(mappedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice))) As List(Of Tuple(Of MediumChanger.Element, BlockDevice))
        If mappedDrives Is Nothing OrElse mappedDrives.Count <= 1 Then Return mappedDrives

        Dim selectedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)) = Nothing
        Dim canceled As Boolean = False
        Invoke(Sub()
                   Using dialog As New ChangerDriveSelectorForm(mappedDrives,
                                                               GetLocalizedString("Batch_SelectDrives_Title"),
                                                               GetLocalizedString("Batch_SelectDrives_Label"),
                                                               GetLocalizedString("Dialog_OK"),
                                                               GetLocalizedString("Dialog_Cancel"))
                       If dialog.ShowDialog(Me) = DialogResult.OK Then
                           selectedDrives = dialog.SelectedDrives
                       Else
                           canceled = True
                       End If
                   End Using
               End Sub)
        If canceled Then Return Nothing
        Return selectedDrives
    End Function

    Private Shared Function DequeueTape(queue As List(Of MediumChanger.Element), queueLock As Object) As MediumChanger.Element
        SyncLock queueLock
            If queue.Count = 0 Then Return Nothing
            Dim tape As MediumChanger.Element = queue(0)
            queue.RemoveAt(0)
            Return tape
        End SyncLock
    End Function

    Private Function GetChangerPath() As String
        Dim changerPath As String = $"\\.\CHANGER{CurrentChanger.DevIndex}"
        If CurrentChanger.device IsNot Nothing Then changerPath = CurrentChanger.device.DevicePath
        Return changerPath
    End Function

    Private Sub AppendLog(message As String)
        If IsDisposed OrElse Not IsHandleCreated Then Exit Sub
        Try
            Invoke(Sub() TextBox1.AppendText($"{message}{Environment.NewLine}"))
        Catch ex As Exception
            Log.Debug(ex, "Failed to append batch log message.")
        End Try
    End Sub

    Private Sub ShowMessageOnUI(message As String, caption As String, icon As MessageBoxIcon)
        If IsDisposed OrElse Not IsHandleCreated Then Exit Sub
        Try
            Invoke(Sub() MessageBox.Show(Me, message, caption, MessageBoxButtons.OK, icon))
        Catch ex As Exception
            Log.Debug(ex, "Failed to show batch message.")
        End Try
    End Sub

    Private Function MoveMediumForBatch(changerPath As String, sourceElement As MediumChanger.Element, destinationElement As MediumChanger.Element) As Boolean
        Return ApplicationWheels.TryExecute(Function() As Byte()
            Dim sense(63) As Byte
            MediumChanger.MoveMedium(changerPath, sourceElement.ElementAddress, destinationElement.ElementAddress, sense, sourceElement.LUN)
            Return sense
        End Function)
    End Function

    Private Sub ReturnTapesToSlots(changerPath As String, mappedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)), tapes As List(Of MediumChanger.Element))
        If mappedDrives Is Nothing OrElse tapes Is Nothing Then Exit Sub
        For index As Integer = 0 To Math.Min(mappedDrives.Count, tapes.Count) - 1
            Try
                MoveMediumForBatch(changerPath, mappedDrives(index).Item1, tapes(index))
            Catch ex As Exception
                AppendLog($"  Failed to return {tapes(index).PrimaryVolumeTagInformation}: {ex.Message}")
                Log.Error(ex, "Failed to return tape {Barcode} to slot.", tapes(index).PrimaryVolumeTagInformation)
            End Try
        Next
    End Sub

    Private Function ProbeMaxExtraPartitionAllowed(drivePath As String) As Byte
        Dim handle As IntPtr = IntPtr.Zero
        Try
            If Not TapeUtils.OpenTapeDrive(drivePath, handle) Then Return 0
            Dim maxExtraPartition As Byte = 0
            Dim modeData As Byte() = TapeUtils.ModeSense(handle, &H11)
            If modeData IsNot Nothing AndAlso modeData.Length >= 3 Then maxExtraPartition = modeData(2)
            If TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream Then maxExtraPartition = 1
            If maxExtraPartition > 1 Then maxExtraPartition = 1
            Return maxExtraPartition
        Catch ex As Exception
            AppendLog($"Probe partition capability failed on {drivePath}: {ex.Message}")
            Log.Debug(ex, "Batch format ModeSense failed.")
            Return 0
        Finally
            If handle <> IntPtr.Zero Then
                Try
                    TapeUtils.LoadEject(handle, TapeUtils.LoadOption.Unthread)
                Catch ex As Exception
                    AppendLog($"Unthread failed: {ex.Message}")
                End Try
                TapeUtils.CloseTapeDrive(handle)
            End If
        End Try
    End Function

    Private Function BuildFormatParamTemplate(maxExtraPartitionAllowed As Byte) As TapeUtils.MKLTFS_Param
        Dim param As New TapeUtils.MKLTFS_Param(maxExtraPartitionAllowed)
        If My.Settings.LTFSWriter_DisablePartition Then param.ExtraPartitionCount = 0
        If param.MaxExtraPartitionAllowed = 0 Then
            If My.Settings.TapeUtils_DriverType = TapeUtils.DriverType.LTO Then param.BlockLen = 65536
        End If
        param.BlockLen = Math.Min(param.BlockLen, TapeUtils.GlobalBlockLimit)
        ' Barcode 与 VolumeLabel 由每盘磁带自己的条码在批量执行时填入
        param.Barcode = ""
        param.VolumeLabel = ""
        Return param
    End Function

    Private Function CloneFormatParam(template As TapeUtils.MKLTFS_Param) As TapeUtils.MKLTFS_Param
        Dim param As New TapeUtils.MKLTFS_Param(template.MaxExtraPartitionAllowed)
        param.BlockLen = template.BlockLen
        param.Capacity = template.Capacity
        param.P0Size = template.P0Size
        param.P1Size = template.P1Size
        param.EncryptionKey = template.EncryptionKey
        param.WORMMode = template.WORMMode
        param.ImmediateMode = template.ImmediateMode
        param.ExtraPartitionCount = template.ExtraPartitionCount
        Return param
    End Function

    Private Function ShowFormatParamDialog(param As TapeUtils.MKLTFS_Param, title As String) As Boolean
        Dim confirmed As Boolean = False
        Invoke(Sub()
                   Using dialog As New SettingPanel With {.SelectedObject = param, .StartPosition = FormStartPosition.Manual, .TopMost = True, .Text = title}
                       dialog.Top = CInt(Top + Height / 2 - dialog.Height / 2)
                       dialog.Left = CInt(Left + Width / 2 - dialog.Width / 2)
                       confirmed = dialog.ShowDialog(Me) = DialogResult.OK
                   End Using
               End Sub)
        Return confirmed
    End Function

    Private Function FormatTapeWithParam(drivePath As String, param As TapeUtils.MKLTFS_Param) As Boolean
        Dim handle As IntPtr = IntPtr.Zero
        If Not TapeUtils.OpenTapeDrive(drivePath, handle) Then
            Throw New Exception($"Cannot open tape drive {drivePath}.")
        End If
        Try
            Dim lastError As String = String.Empty
            Dim formatSucceeded As Boolean = TapeUtils.mkltfs(handle, param.Barcode, param.VolumeLabel, param.ExtraPartitionCount, param.BlockLen, True,
                                                              Sub(message As String) AppendLog($"{param.Barcode}: {message}"),
                                                              Sub(message As String) AppendLog($"{param.Barcode}: {message}"),
                                                              Sub(message As String)
                                                                  lastError = message
                                                                  AppendLog($"{param.Barcode}: {message}")
                                                              End Sub,
                                                              param.Capacity, param.P0Size, param.P1Size, param.EncryptionKey, param.WORMMode)
            If Not formatSucceeded Then
                Throw New Exception($"Format failed: {lastError}")
            End If
            Return True
        Finally
            Try
                TapeUtils.LoadEject(handle, TapeUtils.LoadOption.Unthread)
            Catch ex As Exception
                AppendLog($"Unthread failed: {ex.Message}")
            End Try
            TapeUtils.CloseTapeDrive(handle)
        End Try
    End Function

    Private Function ReadTapeCMReport(drivePath As String, ByRef cmData As TapeUtils.CMParser) As String
        Dim handle As IntPtr = IntPtr.Zero
        If Not TapeUtils.OpenTapeDrive(drivePath, handle) Then
            Throw New Exception($"Cannot open tape drive {drivePath}.")
        End If
        Try
            TapeUtils.LoadEject(handle, TapeUtils.LoadOption.LoadThreaded)
            cmData = New TapeUtils.CMParser(handle)
            Return cmData.GetReport()
        Finally
            Try
                TapeUtils.LoadEject(handle, TapeUtils.LoadOption.Unthread)
            Catch ex As Exception
                AppendLog($"Unthread failed: {ex.Message}")
            End Try
            TapeUtils.CloseTapeDrive(handle)
        End Try
    End Function

    Private Sub 批量格式化ToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles 批量格式化ToolStripMenuItem.Click
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "BatchFormat")
                        If CurrentChanger Is Nothing Then
                            MessageBox.Show(Me, GetLocalizedString("Batch_NoChanger"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Exit Sub
                        End If
                        Log.Information("Batch format started.")
                        Dim noDriveMessage As String = GetLocalizedString("Batch_NoDrive")
                        Dim noTapeMessage As String = GetLocalizedString("Batch_NoTape")
                        Dim confirmTemplate As String = GetLocalizedString("BatchFormat_Confirm")
                        Dim finishedTemplate As String = GetLocalizedString("BatchFormat_Finished")
                        Dim title As String = GetLocalizedString("BatchFormat_Title")
                        Dim parameterTitle As String = GetLocalizedString("BatchFormat_ParamsTitle")
                        SetUILock(True)
                        Task.Run(Sub()
                                     Dim succeeded As Integer = 0
                                     Dim failed As Integer = 0
                                     Dim changerPath As String = String.Empty
                                     Try
                                         AppendLog("Loading elements...")
                                         RefreshCurrentChanger()
                                         changerPath = GetChangerPath()
                                         Dim drives As List(Of MediumChanger.Element) = GetAvailableTapeDrives()
                                         Dim tapes As List(Of MediumChanger.Element) = GetStoredTapeElements()
                                         If drives.Count = 0 Then
                                             ShowMessageOnUI(noDriveMessage, title, MessageBoxIcon.Warning)
                                             Return
                                         End If
                                         If tapes.Count = 0 Then
                                             ShowMessageOnUI(noTapeMessage, title, MessageBoxIcon.Warning)
                                             Return
                                         End If
                                         Dim mappedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)) = GetMappedDriveList(drives)
                                         If mappedDrives.Count = 0 Then
                                             ShowMessageOnUI(noDriveMessage, title, MessageBoxIcon.Warning)
                                             Return
                                         End If
                                         Dim formatTapes As New List(Of MediumChanger.Element)()
                                         For Each tape As MediumChanger.Element In tapes
                                             If String.IsNullOrWhiteSpace(tape.PrimaryVolumeTagInformation) Then
                                                 failed += 1
                                                 AppendLog("Skipped tape without barcode.")
                                                 Continue For
                                             End If
                                             formatTapes.Add(tape)
                                         Next
                                         If formatTapes.Count = 0 Then
                                             RefreshCurrentChanger()
                                             ShowMessageOnUI(String.Format(finishedTemplate, succeeded, failed), title, MessageBoxIcon.Information)
                                             Return
                                         End If
                                         Dim selectedTapes As List(Of MediumChanger.Element) = Nothing
                                         Invoke(Sub()
                                             Using dialog As New ChangerTapeSelectorForm(formatTapes,
                                                                                        GetLocalizedString("BatchFormat_Title"),
                                                                                        GetLocalizedString("BatchFormat_Label"),
                                                                                        GetLocalizedString("Dialog_OK"),
                                                                                        GetLocalizedString("Dialog_Cancel"))
                                                 If dialog.ShowDialog(Me) = DialogResult.OK Then selectedTapes = dialog.SelectedElements
                                             End Using
                                         End Sub)
                                         If selectedTapes Is Nothing OrElse selectedTapes.Count = 0 Then
                                             AppendLog("No tape selected. Batch format cancelled.")
                                             RefreshCurrentChanger()
                                             Return
                                         End If
                                         formatTapes = selectedTapes

                                         Dim selectedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)) = SelectBatchDrives(mappedDrives)
                                         If selectedDrives Is Nothing OrElse selectedDrives.Count = 0 Then
                                             AppendLog("Drive selection cancelled.")
                                             RefreshCurrentChanger()
                                             Return
                                         End If
                                         mappedDrives = selectedDrives

                                         Dim workerCount As Integer = Math.Min(mappedDrives.Count, formatTapes.Count)
                                         Dim loadedTapes As New List(Of MediumChanger.Element)()
                                         Dim minMaxExtraPartition As Byte = Byte.MaxValue
                                        For driveIndex As Integer = 0 To workerCount - 1
                                            Dim driveElement As MediumChanger.Element = mappedDrives(driveIndex).Item1
                                            Dim tape As MediumChanger.Element = formatTapes(driveIndex)
                                            Dim tapeBarcode As String = tape.PrimaryVolumeTagInformation
                                            AppendLog($"Moving {tapeBarcode} to drive {driveElement.Identifier} ...")
                                            Dim moved As Boolean
                                            Try
                                                moved = MoveMediumForBatch(changerPath, tape, driveElement)
                                            Catch ex As Exception
                                                moved = False
                                                Log.Error(ex, "Failed to move tape {Barcode} to drive.", tapeBarcode)
                                            End Try
                                            If Not moved Then
                                                failed += 1
                                                AppendLog($"  Failed to move {tapeBarcode} to drive.")
                                                ReturnTapesToSlots(changerPath, mappedDrives, loadedTapes)
                                                RefreshCurrentChanger()
                                                ShowMessageOnUI(String.Format(finishedTemplate, succeeded, failed), title, MessageBoxIcon.Warning)
                                                Return
                                            End If
                                            loadedTapes.Add(tape)
                                            Dim maxExtraPartition As Byte = ProbeMaxExtraPartitionAllowed(mappedDrives(driveIndex).Item2.DevicePath)
                                            If maxExtraPartition < minMaxExtraPartition Then minMaxExtraPartition = maxExtraPartition
                                        Next
                                         If minMaxExtraPartition = Byte.MaxValue Then minMaxExtraPartition = 0
                                         Dim formatParamTemplate As TapeUtils.MKLTFS_Param = BuildFormatParamTemplate(minMaxExtraPartition)
                                         Dim parametersConfirmed As Boolean = ShowFormatParamDialog(formatParamTemplate, parameterTitle)
                                         If Not parametersConfirmed Then
                                                ReturnTapesToSlots(changerPath, mappedDrives, loadedTapes)
                                                RefreshCurrentChanger()
                                                Return
                                         End If
                                         Dim confirmResult As DialogResult = DialogResult.Cancel
                                         Invoke(Sub() confirmResult = MessageBox.Show(Me, String.Format(confirmTemplate, formatTapes.Count), title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning))
                                         If confirmResult <> DialogResult.OK Then
                                                ReturnTapesToSlots(changerPath, mappedDrives, loadedTapes)
                                                RefreshCurrentChanger()
                                                Return
                                         End If
                                         TapeUtils.AllowPartition = formatParamTemplate.ExtraPartitionCount > 0
                                         Dim pendingTapes As New List(Of MediumChanger.Element)()
                                         If formatTapes.Count > workerCount Then pendingTapes.AddRange(formatTapes.GetRange(workerCount, formatTapes.Count - workerCount))
                                         Dim queueLock As New Object()
                                         Dim workers As New List(Of Task)()
                                         For driveIndex As Integer = 0 To workerCount - 1
                                             Dim driveInfo As Tuple(Of MediumChanger.Element, BlockDevice) = mappedDrives(driveIndex)
                                             Dim initialTape As MediumChanger.Element = loadedTapes(driveIndex)
                                             workers.Add(Task.Run(Sub()
                                                                      Try
                                                                          Dim firstIteration As Boolean = True
                                                                          Dim currentTape As MediumChanger.Element = initialTape
                                                                          Do
                                                                              If firstIteration Then
                                                                                  firstIteration = False
                                                                              Else
                                                                                  currentTape = DequeueTape(pendingTapes, queueLock)
                                                                                  If currentTape Is Nothing Then Exit Do
                                                                                  Dim movedBarcode As String = currentTape.PrimaryVolumeTagInformation
                                                                                  AppendLog($"Moving {movedBarcode} to drive {driveInfo.Item1.Identifier} ...")
                                                                                  Dim moved As Boolean
                                                                                  Try
                                                                                      moved = MoveMediumForBatch(changerPath, currentTape, driveInfo.Item1)
                                                                                  Catch ex As Exception
                                                                                      moved = False
                                                                                      Log.Error(ex, "Failed to move tape to drive.")
                                                                                  End Try
                                                                                  If Not moved Then
                                                                                      Interlocked.Increment(failed)
                                                                                      AppendLog($"  Failed to move {movedBarcode} to drive.")
                                                                                      currentTape = Nothing
                                                                                      Continue Do
                                                                                  End If
                                                                              End If
                                                                              Dim tapeBarcode As String = currentTape.PrimaryVolumeTagInformation
                                                                              Try
                                                                                  Dim tapeParam As TapeUtils.MKLTFS_Param = CloneFormatParam(formatParamTemplate)
                                                                                  tapeParam.Barcode = tapeBarcode
                                                                                  tapeParam.VolumeLabel = tapeBarcode
                                                                                  AppendLog($"Formatting {tapeBarcode} on {driveInfo.Item2.DevicePath} ...")
                                                                                  FormatTapeWithParam(driveInfo.Item2.DevicePath, tapeParam)
                                                                                  Interlocked.Increment(succeeded)
                                                                                  AppendLog($"  Formatted {tapeBarcode}.")
                                                                              Catch ex As Exception
                                                                                  Interlocked.Increment(failed)
                                                                                  AppendLog($"  Failed to format {tapeBarcode}: {ex.Message}")
                                                                                  Log.Error(ex, "Failed to format tape {Barcode}.", tapeBarcode)
                                                                              Finally
                                                                                  If currentTape IsNot Nothing Then
                                                                                      Try
                                                                                          MoveMediumForBatch(changerPath, driveInfo.Item1, currentTape)
                                                                                      Catch ex As Exception
                                                                                          AppendLog($"  Failed to return {tapeBarcode}: {ex.Message}")
                                                                                          Log.Error(ex, "Failed to return tape {Barcode} to slot.", tapeBarcode)
                                                                                      End Try
                                                                                  End If
                                                                                  currentTape = Nothing
                                                                              End Try
                                                                          Loop
                                                                      Catch ex As Exception
                                                                          Interlocked.Increment(failed)
                                                                          AppendLog($"Format worker failed: {ex.Message}")
                                                                          Log.Error(ex, "Batch format worker failed.")
                                                                      End Try
                                                                  End Sub))
                                         Next
                                         Task.WaitAll(workers.ToArray())
                                         RefreshCurrentChanger()
                                         ShowMessageOnUI(String.Format(finishedTemplate, succeeded, failed), title, MessageBoxIcon.Information)
                                     Catch ex As Exception
                                         Log.Error(ex, "Batch format worker failed.")
                                         ShowMessageOnUI(ex.ToString(), title, MessageBoxIcon.Error)
                                     Finally
                                         Invoke(Sub() SetUILock(False))
                                     End Try
                                 End Sub)
                        Log.Information("Batch format worker finished.")
                    End Using
                End Using
            End Using
        End Using
    End Sub

    Private Sub 批量读取CMToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles 批量读取CMToolStripMenuItem.Click
        Using sourceContextScope As IDisposable = LogContext.PushProperty("SourceContext", NameOf(ChangerTool))
            Using categoryScope As IDisposable = LogContext.PushProperty("Category", "Changer")
                Using sessionScope As IDisposable = LogContext.PushProperty("SessionId", _logSessionId)
                    Using eventTypeScope As IDisposable = LogContext.PushProperty("EventType", "BatchReadCM")
                        If CurrentChanger Is Nothing Then
                            MessageBox.Show(Me, GetLocalizedString("Batch_NoChanger"), GetLocalizedString("BatchReadCM_Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Exit Sub
                        End If
                        Log.Information("Batch CM read started.")
                        Dim noDriveMessage As String = GetLocalizedString("Batch_NoDrive")
                        Dim noTargetMessage As String = GetLocalizedString("BatchReadCM_NoTarget")
                        Dim confirmTemplate As String = GetLocalizedString("BatchReadCM_Confirm")
                        Dim finishedTemplate As String = GetLocalizedString("BatchReadCM_Finished")
                        Dim title As String = GetLocalizedString("BatchReadCM_Title")
                        SetUILock(True)
                        Task.Run(Sub()
                                     Dim succeeded As Integer = 0
                                     Dim failed As Integer = 0
                                     Dim mismatched As Integer = 0
                                     Dim changerPath As String = String.Empty
                                     Try
                                         AppendLog("Loading elements...")
                                         RefreshCurrentChanger()
                                         changerPath = GetChangerPath()
                                         Dim drives As List(Of MediumChanger.Element) = GetAvailableTapeDrives()
                                         Dim tapes As List(Of MediumChanger.Element) = GetStoredTapeElements()
                                         If drives.Count = 0 Then
                                             ShowMessageOnUI(noDriveMessage, title, MessageBoxIcon.Warning)
                                             Return
                                         End If
                                         Dim mappedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)) = GetMappedDriveList(drives)
                                         If mappedDrives.Count = 0 Then
                                             ShowMessageOnUI(noDriveMessage, title, MessageBoxIcon.Warning)
                                             Return
                                         End If
                                         Dim targetTapes As New List(Of MediumChanger.Element)()
                                         For Each tape As MediumChanger.Element In tapes
                                             Dim tapeBarcode As String = tape.PrimaryVolumeTagInformation
                                             If String.IsNullOrWhiteSpace(tapeBarcode) Then Continue For
                                             tapeBarcode = tapeBarcode.Trim()
                                             If LocalCMExists(tapeBarcode) Then Continue For
                                             targetTapes.Add(tape)
                                         Next
                                         If targetTapes.Count = 0 Then
                                             RefreshCurrentChanger()
                                             ShowMessageOnUI(noTargetMessage, title, MessageBoxIcon.Information)
                                             Return
                                         End If
                                         Dim selectedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)) = SelectBatchDrives(mappedDrives)
                                         If selectedDrives Is Nothing OrElse selectedDrives.Count = 0 Then
                                             AppendLog("Drive selection cancelled.")
                                             RefreshCurrentChanger()
                                             Return
                                         End If
                                         mappedDrives = selectedDrives

                                         Dim confirmResult As DialogResult = DialogResult.Cancel
                                         Invoke(Sub() confirmResult = MessageBox.Show(Me, String.Format(confirmTemplate, targetTapes.Count), title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning))
                                         If confirmResult <> DialogResult.OK Then Return
                                         Dim workerCount As Integer = Math.Min(mappedDrives.Count, targetTapes.Count)
                                         Dim pendingTapes As New List(Of MediumChanger.Element)(targetTapes)
                                         Dim queueLock As New Object()
                                         Dim workers As New List(Of Task)()
                                         For driveIndex As Integer = 0 To workerCount - 1
                                             Dim driveInfo As Tuple(Of MediumChanger.Element, BlockDevice) = mappedDrives(driveIndex)
                                             workers.Add(Task.Run(Sub()
                                                                      Try
                                                                          Do
                                                                              Dim currentTape As MediumChanger.Element = DequeueTape(pendingTapes, queueLock)
                                                                              If currentTape Is Nothing Then Exit Do
                                                                              Dim tapeBarcode As String = currentTape.PrimaryVolumeTagInformation.Trim()
                                                                              AppendLog($"Moving {tapeBarcode} to drive {driveInfo.Item1.Identifier} ...")
                                                                              Dim moved As Boolean
                                                                              Try
                                                                                  moved = MoveMediumForBatch(changerPath, currentTape, driveInfo.Item1)
                                                                              Catch ex As Exception
                                                                                  moved = False
                                                                                  Log.Error(ex, "Failed to move tape to drive.")
                                                                              End Try
                                                                              If Not moved Then
                                                                                  Interlocked.Increment(failed)
                                                                                  AppendLog($"  Failed to move {tapeBarcode} to drive.")
                                                                                  Continue Do
                                                                              End If
                                                                              Try
                                                                                  AppendLog($"Reading CM from {tapeBarcode} on {driveInfo.Item2.DevicePath} ...")
                                                                                  Dim cmData As TapeUtils.CMParser = Nothing
                                                                                  Dim report As String = ReadTapeCMReport(driveInfo.Item2.DevicePath, cmData)
                                                                                  If String.IsNullOrWhiteSpace(report) Then Throw New Exception("CM report is empty.")
                                                                                  Dim cmBarcode As String = Nothing
                                                                                  If cmData IsNot Nothing AndAlso cmData.ApplicationSpecificData IsNot Nothing Then cmBarcode = cmData.ApplicationSpecificData.Barcode
                                                                                  If Not String.IsNullOrWhiteSpace(cmBarcode) Then
                                                                                      cmBarcode = cmBarcode.Trim()
                                                                                      If Not String.Equals(cmBarcode, tapeBarcode, StringComparison.OrdinalIgnoreCase) Then
                                                                                          Interlocked.Increment(mismatched)
                                                                                          AppendLog($"  Barcode mismatch: slot={tapeBarcode} CM={cmBarcode}")
                                                                                      End If
                                                                                  Else
                                                                                      AppendLog($"  CM barcode unavailable: {tapeBarcode}")
                                                                                  End If
                                                                                  Dim savedPath As String = SaveLocalCMReport(tapeBarcode, report)
                                                                                  Interlocked.Increment(succeeded)
                                                                                  AppendLog($"  Saved CM: {savedPath}")
                                                                              Catch ex As Exception
                                                                                  Interlocked.Increment(failed)
                                                                                  AppendLog($"  Failed to read CM from {tapeBarcode}: {ex.Message}")
                                                                                  Log.Error(ex, "Failed to read CM from tape {Barcode}.", tapeBarcode)
                                                                              Finally
                                                                                  Try
                                                                                      MoveMediumForBatch(changerPath, driveInfo.Item1, currentTape)
                                                                                  Catch ex As Exception
                                                                                      AppendLog($"  Failed to return {tapeBarcode}: {ex.Message}")
                                                                                      Log.Error(ex, "Failed to return tape {Barcode} to slot.", tapeBarcode)
                                                                                  End Try
                                                                              End Try
                                                                          Loop
                                                                      Catch ex As Exception
                                                                          Interlocked.Increment(failed)
                                                                          AppendLog($"CM read worker failed: {ex.Message}")
                                                                          Log.Error(ex, "Batch CM read worker failed.")
                                                                      End Try
                                                                  End Sub))
                                         Next
                                         Task.WaitAll(workers.ToArray())
                                         RefreshCurrentChanger()
                                         ShowMessageOnUI(String.Format(finishedTemplate, succeeded, failed, mismatched), title, MessageBoxIcon.Information)
                                     Catch ex As Exception
                                         Log.Error(ex, "Batch CM read worker failed.")
                                         ShowMessageOnUI(ex.ToString(), title, MessageBoxIcon.Error)
                                     Finally
                                         Invoke(Sub() SetUILock(False))
                                     End Try
                                 End Sub)
                        Log.Information("Batch CM read worker finished.")
                    End Using
                End Using
            End Using
        End Using
    End Sub

    Private Sub 文件管理器ToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles 文件管理器ToolStripMenuItem.Click
        If CurrentChanger Is Nothing Then
            MessageBox.Show(Me, GetLocalizedString("Batch_NoChanger"), GetLocalizedString("FileManager_Title"), MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Using dialog As New ChangerFileManagerForm(CurrentChanger, GetChangerPath())
            dialog.ShowDialog(Me)
        End Using

        If LoadComplete AndAlso CheckBox1.Checked Then
            SetUILock(True)
            Task.Run(Sub()
                         Try
                             RefreshCurrentChanger()
                         Catch ex As Exception
                             Log.Error(ex, "Failed to refresh changer after closing file manager.")
                         Finally
                             Invoke(Sub()
                                        SwitchChanger()
                                        SetUILock(False)
                                    End Sub)
                         End Try
                     End Sub)
        End If
    End Sub

    Private Sub Button4_MouseUp(sender As Object, e As MouseEventArgs) Handles Button4.MouseUp
        ContextMenuStrip1.Show(Button4, e.X, e.Y)
    End Sub
End Class

Friend Class ChangerTapeSelectorForm
    Inherits Form

    Private ReadOnly _list As New CheckedListBox()
    Private ReadOnly _elements As New List(Of MediumChanger.Element)

    Public ReadOnly Property SelectedElements As List(Of MediumChanger.Element)
        Get
            Dim selected As New List(Of MediumChanger.Element)
            For Each index As Integer In _list.CheckedIndices
                selected.Add(_elements(index))
            Next
            Return selected
        End Get
    End Property

    Public Sub New(tapes As List(Of MediumChanger.Element), title As String, label As String, okText As String, cancelText As String)
        Text = title
        Size = New Drawing.Size(420, 360)
        StartPosition = FormStartPosition.CenterParent
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        Dim topPanel As New Panel() With {.Dock = DockStyle.Top, .Height = 40}
        Dim labelControl As New Label() With {.Dock = DockStyle.Fill, .Text = label, .TextAlign = Drawing.ContentAlignment.MiddleLeft, .Padding = New Padding(8, 0, 8, 0)}
        topPanel.Controls.Add(labelControl)
        _list.Dock = DockStyle.Fill
        _list.CheckOnClick = True
        _list.IntegralHeight = False
        For Each tape As MediumChanger.Element In tapes
            _elements.Add(tape)
            Dim barcode As String = If(tape.PrimaryVolumeTagInformation, "").Trim()
            _list.Items.Add($"{barcode} @0x{Hex(tape.ElementAddress).PadLeft(4, "0"c)} LUN{tape.LUN}")
        Next
        Dim bottomPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Bottom, .Height = 44, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(0, 8, 8, 0)}
        Dim okButton As New Button() With {.Text = okText, .DialogResult = DialogResult.OK, .Size = New Drawing.Size(80, 26)}
        Dim cancelButton As New Button() With {.Text = cancelText, .DialogResult = DialogResult.Cancel, .Size = New Drawing.Size(80, 26)}
        bottomPanel.Controls.Add(okButton)
        bottomPanel.Controls.Add(cancelButton)
        AcceptButton = okButton
        CancelButton = cancelButton
        Controls.Add(_list)
        Controls.Add(topPanel)
        Controls.Add(bottomPanel)
    End Sub
End Class


Friend Class ChangerDriveSelectorForm
    Inherits Form

    Private ReadOnly _list As New CheckedListBox()
    Private ReadOnly _drives As New List(Of Tuple(Of MediumChanger.Element, BlockDevice))
    Private ReadOnly _okButton As New Button()

    Public ReadOnly Property SelectedDrives As List(Of Tuple(Of MediumChanger.Element, BlockDevice))
        Get
            Dim selected As New List(Of Tuple(Of MediumChanger.Element, BlockDevice))
            For Each index As Integer In _list.CheckedIndices
                selected.Add(_drives(index))
            Next
            Return selected
        End Get
    End Property

    Public Sub New(drives As List(Of Tuple(Of MediumChanger.Element, BlockDevice)), title As String, label As String, okText As String, cancelText As String)
        Text = title
        Size = New Drawing.Size(560, 340)
        StartPosition = FormStartPosition.CenterParent
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        Dim topPanel As New Panel() With {.Dock = DockStyle.Top, .Height = 40}
        Dim labelControl As New Label() With {.Dock = DockStyle.Fill, .Text = label, .TextAlign = Drawing.ContentAlignment.MiddleLeft, .Padding = New Padding(8, 0, 8, 0)}
        topPanel.Controls.Add(labelControl)
        _list.Dock = DockStyle.Fill
        _list.CheckOnClick = True
        _list.IntegralHeight = False
        For Each drive As Tuple(Of MediumChanger.Element, BlockDevice) In drives
            _drives.Add(drive)
            Dim address As String = Hex(drive.Item1.ElementAddress).PadLeft(4, "0"c)
            _list.Items.Add($"{drive.Item2.DevicePath}  @0x{address}  [{drive.Item2.SerialNumber}]", True)
        Next
        _okButton.Text = okText
        _okButton.DialogResult = DialogResult.OK
        _okButton.Size = New Drawing.Size(80, 26)
        AddHandler _list.ItemCheck, Sub(sender As Object, e As ItemCheckEventArgs)
                                        Dim checkedCount As Integer = _list.CheckedIndices.Count
                                        If e.NewValue = CheckState.Checked Then
                                            checkedCount += 1
                                        ElseIf e.NewValue = CheckState.Unchecked Then
                                            checkedCount -= 1
                                        End If
                                        _okButton.Enabled = checkedCount > 0
                                    End Sub
        Dim cancelButton As New Button() With {.Text = cancelText, .DialogResult = DialogResult.Cancel, .Size = New Drawing.Size(80, 26)}
        Dim bottomPanel As New FlowLayoutPanel() With {.Dock = DockStyle.Bottom, .Height = 44, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(0, 8, 8, 0)}
        bottomPanel.Controls.Add(_okButton)
        bottomPanel.Controls.Add(cancelButton)
        AcceptButton = _okButton
        CancelButton = cancelButton
        Controls.Add(_list)
        Controls.Add(topPanel)
        Controls.Add(bottomPanel)
    End Sub
End Class