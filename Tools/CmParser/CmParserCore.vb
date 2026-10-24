Option Strict On
Option Infer On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text

Public Class TapeUtils
    Public Class CMParser
        Const GUARD_WRAP_IDENTIFIER As Integer = &HFFFFFFFE
        Const UNUSED_WRAP_IDENTIFIER As Integer = &HFFFFFFFF
        Public Property a_CMBuffer As Byte() = Array.Empty(Of Byte)()

Public Property a_PageID As Integer
Public Property a_Offset As Integer
        Public Property a_UnProt As Integer = 0
Public Property a_Key As Integer
Public a_Index As Integer
        Public Property a_Err As Integer = 0
Public Property a_Buffer As Byte()
Public Property at_Offset As Byte()
Public Property a_Length As Integer = 0
        Public Property a_CleansRemaining As Integer = 0
        Public Property a_CleanLength As Double
        Public Property a_NWraps As Integer = 0
        Public Property a_TapeDirLength As Integer = 16
        Public Property a_SetsPerWrap As Integer = 0
Public Property a_SetID As Integer = 0
Public Property a_LastID As Integer = 0
        Public Property a_Barcode As String
Public Property a_AttributeID As Integer
Public Property a_AttributeLength As Integer
        Public Property a_HdrLength As Integer
        Public Property a_DriveTypeIdentifier As Integer = 0
Public Property a_OutputStr As String = ""
        Public Property a_TapeWritePassPartition As Integer = 0
        Public Property a_NumPartitions As New List(Of EOD)
Public a_PartitionKey As Integer
Public Property a_set As Integer
        Public Property g_ValidCM As Boolean = False
        Public Property g_Channels As Integer = 8
        Public Property g_LoadCount As Integer = 0
        Public Property g_CartridgeSN As String = "          "
        Public Property g_TPM As Integer = 0
        Public Property g_Barcode As String = "        "
        Public Property g_DHLTimeStamp As Integer = 0
        Public Property g_FaultLogSize As Integer = 0
        Public Property g_LtoPearlFlagEnable As Integer = 0
        Public Property g_DHLPowerCount As Integer = 0
        Public Property g_DHLPocCount As Integer = 0


        Public Property PageData As New List(Of Page)
        Public Property CartridgeMfgData As New Cartridge_mfg
        Public Property MediaMfgData As New Media_mfg
        Public Property a_UsageData As New List(Of UsagePage)
        Public Property UsageData As New List(Of Usage)
        Public Property StatusData As New TapeStatus
        Public Property InitialisationData As New Initialisation
        Public Property PartitionEOD As New List(Of EOD)
        Public Property CartridgeContentData As New CartridgeContent
        Public Property TapeDirectoryData As New TapeDirectory
        Public Property SuspendWriteData As New SuspendWrite
        Public Property ApplicationSpecificData As New ApplicationSpecific
        Public Function a_Usage(index As Integer, Optional ByVal CreateNew As Boolean = True) As UsagePage
            For Each up As UsagePage In a_UsageData
                If up.index = index Then Return up
            Next
            If CreateNew Then
                Dim upn As New UsagePage With {.index = index}
                a_UsageData.Add(upn)
                Return upn
            End If
            Return Nothing
        End Function
        <Serializable> Public Class UsagePage
            Public Property index As Integer
            Public Property data0 As Byte()
            Public Property data1 As Integer
        End Class
        <Serializable> Public Class Page
            Public Property a_Key As Integer
            Public Property Version As Integer
            Public Property Offset As Integer = -1
            Public Property Length As Integer = -1
            Public Property Type As TypeDef
            Public Enum TypeDef
                unprotected
                [protected]
            End Enum
        End Class
        <Serializable> Public Class Cartridge_mfg
            Public Property TapeVendor As String
            Public Property CartridgeSN As String
            Public Property CartridgeType As Integer
            Public Property Format As String = ""
            Public ReadOnly Property IsLTO3Plus As Boolean
                Get
                    Dim fmt As String = Format
                    Return fmt IsNot Nothing AndAlso fmt.Contains("LTO-3") OrElse fmt.Contains("LTO-4") OrElse fmt.Contains("LTO-5") OrElse fmt.Contains("LTO-6") OrElse fmt.Contains("LTO-7") OrElse fmt.Contains("LTO-8") OrElse fmt.Contains("LTO-9")
                End Get
            End Property
            Public ReadOnly Property IsLTO4Plus As Boolean
                Get
                    Dim fmt As String = Format
                    Return fmt IsNot Nothing AndAlso fmt.Contains("LTO-4") OrElse fmt.Contains("LTO-5") OrElse fmt.Contains("LTO-6") OrElse fmt.Contains("LTO-7") OrElse fmt.Contains("LTO-8") OrElse fmt.Contains("LTO-9")
                End Get
            End Property
            Public ReadOnly Property IsLTO5Plus As Boolean
                Get
                    Dim fmt As String = Format
                    Return fmt IsNot Nothing AndAlso fmt.Contains("LTO-5") OrElse fmt.Contains("LTO-6") OrElse fmt.Contains("LTO-7") OrElse fmt.Contains("LTO-8") OrElse fmt.Contains("LTO-9")
                End Get
            End Property
            Public ReadOnly Property IsLTO6Plus As Boolean
                Get
                    Dim fmt As String = Format
                    Return fmt IsNot Nothing AndAlso fmt.Contains("LTO-6") OrElse fmt.Contains("LTO-7") OrElse fmt.Contains("LTO-8") OrElse fmt.Contains("LTO-9")
                End Get
            End Property
            Public ReadOnly Property IsLTO7Plus As Boolean
                Get
                    Dim fmt As String = Format
                    Return fmt IsNot Nothing AndAlso fmt.Contains("LTO-7") OrElse fmt.Contains("LTO-8") OrElse fmt.Contains("LTO-9")
                End Get
            End Property
            Public ReadOnly Property IsLTO8Plus As Boolean
                Get
                    Dim fmt As String = Format
                    Return fmt IsNot Nothing AndAlso fmt.Contains("LTO-8") OrElse fmt.Contains("LTO-9")
                End Get
            End Property
            Public ReadOnly Property IsLTO9Plus As Boolean
                Get
                    Dim fmt As String = Format
                    Return fmt IsNot Nothing AndAlso fmt.Contains("LTO-9")
                End Get
            End Property
            Public ReadOnly Property CartridgeTypeAbbr As String
                Get
                    If ((CartridgeType >> 15) And 1) = 1 Then Return "CU"
                    Select Case CartridgeType And &HFF
                        Case 1
                            Return "L1"
                        Case 2
                            Return "L2"
                        Case 4
                            Return "L3"
                        Case 8
                            Return "L4"
                        Case 16
                            Return "L5"
                        Case 32
                            Return "L6"
                        Case 64
                            If Format.Contains("Type M") Then
                                Return "M8"
                            Else
                                Return "L7"
                            End If
                        Case 128
                            Return "L8"
                        Case 129
                            Return "L9"
                        Case Else
                            Select Case CartridgeType
                                Case 5126
                                    Return "JA"
                                Case 13
                                    Return "JB"
                                Case 15
                                    Return "JC"
                                Case 17
                                    Return "JD"
                                Case 19
                                    Return "JE"
                                Case 21
                                    Return "JF"
                                Case 13318
                                    Return "JJ"
                                Case 8207
                                    Return "JK"
                                Case 8209
                                    Return "JL"
                                Case 8211
                                    Return "JM"
                                Case 8213
                                    Return "JN"
                            End Select
                    End Select
                    Return ""
                End Get
            End Property
            Public Shared Function GetCMLength(Abbr As String) As Integer
                Select Case Abbr.ToUpper
                    Case "CU"
                        Return 4096
                    Case "L1"
                        Return 4096
                    Case "L2"
                        Return 4096
                    Case "L3"
                        Return 4096
                    Case "L4"
                        Return 8160
                    Case "L5"
                        Return 8160
                    Case "L6"
                        Return 16352
                    Case "L7"
                        Return 16352
                    Case "M8"
                        Return 16352
                    Case "L8"
                        Return 16352
                    Case "L9"
                        Return 32736
                    Case Else
                        Return 0
                End Select
            End Function
            Public Property KB_PER_DATASET As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 404
                        Case "L2"
                            Return 404
                        Case "L3"
                            Return 1617
                        Case "L4"
                            Return 1590
                        Case "L5"
                            Return 2473
                        Case "L6"
                            Return 2473
                        Case "L7"
                            Return 5032
                        Case "M8"
                            Return 5032
                        Case "L8"
                            Return 5032
                        Case "L9"
                            Return 9806
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property CCQ_PER_DATASET As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 64
                        Case "L2"
                            Return 64
                        Case "L3"
                            Return 128
                        Case "L4"
                            Return 128
                        Case "L5"
                            Return 192
                        Case "L6"
                            Return 192
                        Case "L7"
                            Return 192
                        Case "M8"
                            Return 192
                        Case "L8"
                            Return 192
                        Case "L9"
                            Return 384
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property SETS_PER_WRAP As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 5500
                        Case "L2"
                            Return 8200
                        Case "L3"
                            Return 6000
                        Case "L4"
                            Return 9500
                        Case "L5"
                            Return 7800
                        Case "L6"
                            Return 7805
                        Case "L7"
                            Return 10950
                        Case "M8"
                            Return 10950
                        Case "L8"
                            Return 11660
                        Case "L9"
                            Return 6770
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property MB_PER_WRAP_METRE As Double
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 3.84
                        Case "L2"
                            Return 5.75
                        Case "L3"
                            Return 14.98
                        Case "L4"
                            Return 19.27
                        Case "L5"
                            Return 23.9
                        Case "L6"
                            Return 23.89
                        Case "L7"
                            Return 59.85
                        Case "M8"
                            Return 59.85
                        Case "L8"
                            Return 63.64
                        Case "L9"
                            Return 66.59
                    End Select
                    Return 0
                End Get
                Set(value As Double)
                End Set
            End Property
            Public Property TAPE_LU_LIFE As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 20000
                        Case "L2"
                            Return 20000
                        Case "L3"
                            Return 20000
                        Case "L4"
                            Return 20000
                        Case "L5"
                            Return 20000
                        Case "L6"
                            Return 20000
                        Case "L7"
                            Return 20000
                        Case "M8"
                            Return 20000
                        Case "L8"
                            Return 20000
                        Case "L9"
                            Return 20000
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property TAPE_LIFE_IN_VOLS As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 260
                        Case "L2"
                            Return 260
                        Case "L3"
                            Return 260
                        Case "L4"
                            Return 260
                        Case "L5"
                            Return 260
                        Case "L6"
                            Return 130
                        Case "L7"
                            Return 130
                        Case "M8"
                            Return 98
                        Case "L8"
                            Return 75
                        Case "L9"
                            Return 55
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property WRAP_LEN_IN_MTRS As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 580
                        Case "L2"
                            Return 580
                        Case "L3"
                            Return 648
                        Case "L4"
                            Return 783
                        Case "L5"
                            Return 808
                        Case "L6"
                            Return 808
                        Case "L7"
                            Return 922
                        Case "M8"
                            Return 922
                        Case "L8"
                            Return 922
                        Case "L9"
                            Return 997
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property TAPE_LEN_IN_MTRS As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 609
                        Case "L2"
                            Return 609
                        Case "L3"
                            Return 680
                        Case "L4"
                            Return 820
                        Case "L5"
                            Return 846
                        Case "L6"
                            Return 846
                        Case "L7"
                            Return 960
                        Case "M8"
                            Return 960
                        Case "L8"
                            Return 960
                        Case "L9"
                            Return 1034
                        Case "CU"
                            Return 319
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property NO_WRAPS_ON_TAPE As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 48
                        Case "L2"
                            Return 64
                        Case "L3"
                            Return 44
                        Case "L4"
                            Return 56
                        Case "L5"
                            Return 80
                        Case "L6"
                            Return 136
                        Case "L7"
                            Return 112
                        Case "M8"
                            Return 168
                        Case "L8"
                            Return 208
                        Case "L9"
                            Return 280
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property MIN_DATASETS_FOR_ASSESSING_CAPACITY_LOSS As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return 11064
                        Case "L2"
                            Return 16600
                        Case "L3"
                            Return 12500
                        Case "L4"
                            Return 19500
                        Case "L5"
                            Return 15920
                        Case "L6"
                            Return 15620
                        Case "L7"
                            Return 11060
                        Case "M8"
                            Return 11060
                        Case "L8"
                            Return 12020
                        Case "L9"
                            Return 13540
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property DENSITY_CODE As Integer
                Get
                    Select Case CartridgeTypeAbbr
                        Case "L1"
                            Return &H40
                        Case "L2"
                            Return &H42
                        Case "L3"
                            Return &H44
                        Case "L4"
                            Return &H46
                        Case "L5"
                            Return &H58
                        Case "L6"
                            Return &H5A
                        Case "L7"
                            Return &H5C
                        Case "M8"
                            Return &H5D
                        Case "L8"
                            Return &H5E
                        Case "L9"
                            Return &H60
                    End Select
                    Return 0
                End Get
                Set(value As Integer)
                End Set
            End Property
            Public Property KB_PER_WRAP As Integer
                Get
                    Return KB_PER_DATASET * SETS_PER_WRAP
                End Get
                Set(value As Integer)
                End Set
            End Property

            Public Property MfgDate As String
            Public Property TapeLength As Integer = 0
            Public Property MediaCode As Integer
            Public Property ParticleType As particle
            Public Property SubstrateType As substrate
            Public Property Servo_Band_ID As svbid
            Public Enum particle
                MP
                BaFe
            End Enum
            Public Enum substrate
                PEN
                SPALTAN
            End Enum
            Public Enum svbid
                legacy_UDIM
                non_UDIM
            End Enum
        End Class
        <Serializable> Public Class Usage
            Public Property Index As Integer

            Public Property PageID As Integer
            Public Property DrvSN As String
            Public Property ThreadCount As Integer
            Public Property SetsWritten As Long
            Public Property SetsRead As Long
            Public Property TotalSets As Long
            Public Property WriteRetries As Integer
            Public Property ReadRetries As Integer
            Public Property UnRecovWrites As Integer
            Public Property UnRecovReads As Integer
            Public Property SuspendedWrites As Integer
            Public Property FatalSusWrites As Integer
            Public Property SuspendedAppendWrites As Integer
            Public Property LP3Passes As Integer
            Public Property MidpointPasses As Integer
            Public Property MaxTapeTemp As Integer

            Public Property CCQWriteFails As Integer
            Public Property C2RecovErrors As Integer
            Public Property DirectionChanges As Integer
            Public Property TapePullingTime As Integer
            Public Property TapeMetresPulled As Integer
            Public Property Repositions As Integer
            Public Property TotalLoadUnloads As Integer
            Public Property StreamFails As Integer

            Public Property MaxDriveTemp As Double
            Public Property MinDriveTemp As Double

            Public Property LifeSetsWritten As Integer
            Public Property LifeSetsRead As Integer
            Public Property LifeWriteRetries As Integer
            Public Property LifeReadRetries As Integer
            Public Property LifeUnRecovWrites As Integer
            Public Property LifeUnRecovReads As Integer
            Public Property LifeSuspendedWrites As Integer
            Public Property LifeFatalSuspWrites As Integer
            Public Property LifeTapeMetresPulled As Integer

            Public Property LifeSuspAppendWrites As Integer
            Public Property LifeLP3Passes As Integer
            Public Property LifeMidpointPasses As Integer

        End Class
        <Serializable> Public Class Media_mfg
            Public Property Version As Byte
            Public Property MediaMfgDate As String
            Public Property MediaVendor As String
            Public ReadOnly Property MediaVendor_Code As Integer
                Get
                    With MediaVendor.ToUpper
                        If .Contains("SONY") Then Return 1
                        If .Contains("TDK") Then Return 2
                        If .Contains("FUJI") Then Return 3
                        If .Contains("MAXELL") Then Return 4
                        If .Contains("IMATION") Then Return 5
                        If .Contains("EMTEC") Then Return 6
                        Return 0
                    End With
                End Get
            End Property
        End Class
        <Serializable> Public Class TapeStatus
            Public Property ThreadCount As Integer
            Public Property EncryptedData As Boolean
            Public Property LastLocation As Integer = 0
        End Class
        <Serializable> Public Class Initialisation
            Public Property LP1 As Integer
            Public Property LP3 As Integer
            Public Property LP5 As Integer
        End Class
        <Serializable> Public Class EOD
            Public Property Partition As Integer
            Public Property Dataset As Integer
            Public Property WrapNumber As Integer
            Public Property Validity As Integer
            Public Property PhysicalPosition As Integer
        End Class
        <Serializable> Public Class CartridgeContent
            Public Property Drive_Id As String
            Public Property Cartridge_Content As Integer
            Public Property PartitionedCartridge As Boolean
            Public Property Type_M_Cartridge As Boolean
            Public Property Drive_Firmware_Id As String
        End Class
        <Serializable> Public Class TapeDirectory
            Public Property Version As Byte
            Public Property FID_Tape_Write_Pass_Partition_0 As Integer
            Public Property FID_Tape_Write_Pass_Partition_1 As Integer
            Public Property FID_Tape_Write_Pass_Partition_2 As Integer
            Public Property FID_Tape_Write_Pass_Partition_3 As Integer
            Public Property Wrap As String
            Public Property WrapEntryInfo As New List(Of WrapEntryItemSet)
            Public Property CapacityLoss As New List(Of Double)
            Public Property DatasetsOnWrapData As New List(Of Dataset)
            <Serializable> Public Class Dataset
                Public Property Index As Integer
                Public Property Data As Integer
            End Class
            <Serializable> Public Class WrapEntryItemSet
                Public Property Index As Integer
                Public Property Content As String
                Public Property RawData As Integer()
                Public Property RecCount As Integer
                Public Property FileMarkCount As Integer
            End Class
            Public Function WrapEntry(Index As Integer, Optional ByVal CreateNew As Boolean = True) As WrapEntryItemSet
                For Each d As WrapEntryItemSet In WrapEntryInfo
                    If d.Index = Index Then Return d
                Next
                If CreateNew Then
                    Dim dn As New WrapEntryItemSet With {.Index = Index}
                    WrapEntryInfo.Add(dn)
                    Return dn
                End If
                Return Nothing
            End Function
            Public Function DatasetsOnWrap(Index As Integer, Optional ByVal CreateNew As Boolean = True) As Dataset
                For Each d As Dataset In DatasetsOnWrapData
                    If d.Index = Index Then Return d
                Next
                If CreateNew Then
                    Dim dn As New Dataset With {.Index = Index}
                    DatasetsOnWrapData.Add(dn)
                    Return dn
                End If
                Return Nothing
            End Function
        End Class
        <Serializable> Public Class SuspendWrite
            Public Function DataSetID(Index As Integer, Optional ByVal CreateNew As Boolean = True) As DataInfo
                For Each di As DataInfo In DataSetList
                    If di.Index = Index Then Return di
                Next
                If CreateNew Then
                    Dim din As New DataInfo With {.Index = Index}
                    DataSetList.Add(din)
                    Return din
                End If
                Return Nothing
            End Function
            Public Function WTapePass(Index As Integer, Optional ByVal CreateNew As Boolean = True) As DataInfo
                For Each di As DataInfo In WTapePassList
                    If di.Index = Index Then Return di
                Next
                If CreateNew Then
                    Dim din As New DataInfo With {.Index = Index}
                    WTapePassList.Add(din)
                    Return din
                End If
                Return Nothing
            End Function
            Public Property DataSetList As New List(Of DataInfo)
            Public Property WTapePassList As New List(Of DataInfo)
            <Serializable> Public Class DataInfo
                Public Property Index As Integer
                Public Property Value As Integer
            End Class
        End Class
        <Serializable> Public Class ApplicationSpecific
            Public Property Barcode As String
            Public Property Application_vendor As String
            Public Property Application_name As String
            Public Property Application_version As String
        End Class
        Public Enum gtype
            page
            cartridge_mfg
            media_mfg
            usage
            status
            initialisation
            EOD
            cartridge_content
            tape_directory
            suspended_writes
            application_specific
        End Enum
        Public Function g_CM(gtype As gtype, Optional ByVal gKey As Integer = 0, Optional ByVal createNew As Boolean = True) As Object
            Select Case gtype
                Case gtype.page
                    For Each p As Page In PageData
                        If p.a_Key = gKey Then Return p
                    Next
                    If createNew Then
                        Dim pnew As New Page With {.a_Key = gKey}
                        PageData.Add(pnew)
                        Return pnew
                    Else
                        Return Nothing
                    End If
                Case gtype.cartridge_mfg
                    Return CartridgeMfgData
                Case gtype.media_mfg
                    Return MediaMfgData
                Case gtype.usage
                    For Each u As Usage In UsageData
                        If u.Index = gKey Then Return u
                    Next
                    If createNew Then
                        Dim unew As New Usage With {.Index = gKey}
                        UsageData.Add(unew)
                        Return unew
                    End If
                Case gtype.status
                    Return StatusData
                Case gtype.initialisation
                    Return InitialisationData
                Case gtype.EOD
                    For Each eod As EOD In PartitionEOD
                        If eod.Partition = gKey Then Return eod
                    Next
                    If createNew Then
                        Dim eodn As New EOD With {.Partition = gKey}
                        PartitionEOD.Add(eodn)
                        Return eodn
                    End If
                Case gtype.cartridge_content
                    Return CartridgeContentData
                Case gtype.tape_directory
                    Return TapeDirectoryData
                Case gtype.suspended_writes
                    Return SuspendWriteData
                Case gtype.application_specific
                    Return ApplicationSpecificData
            End Select
            Return Nothing
        End Function
        Public Function RunParse(Optional ByVal Warn As Action(Of String) = Nothing) As Boolean
            If Warn Is Nothing Then
                Warn = Sub(text As String)
                       End Sub
            End If
            ' verify the checksum byte 4 is the xor of the first 4 bytes, otherwise CM data is not valid so don't parse the dataset
            ' also check that byte 5 (the size of CM in 1K units) is 0x04 (LTO1/2/3) or 0x08 (LTO4/5)
            If a_CMBuffer.Length < &H1000 Then
                If a_CMBuffer.Length <> 0 Then
                    Warn($"invalid CM buffer size (0x{Hex(a_CMBuffer.Length)}){vbCrLf}")
                End If
                Return False
            ElseIf ((a_CMBuffer(0) Xor a_CMBuffer(1) Xor a_CMBuffer(2) Xor a_CMBuffer(3)) <> a_CMBuffer(4)) Then
                Warn($"invalid CM checksum byte with length {a_CMBuffer.Length}{vbCrLf}")
                Return False
            Else
                ' Check the CM size byte, located at offset 5.
                a_Length = a_CMBuffer(5)
                ' CR 11775 : Changes For LTO9
                ' --------------------------------------------------------------------
                ' LTO9 has a 32kB CM (length Byte 0x20) so allow For that As well.
                If (a_Length <> 4 AndAlso a_Length <> 8 AndAlso a_Length <> 16 AndAlso a_Length <> 32) Then
                    If a_Length <> 0 Then ' only warn if there's a strange non-zero size
                        Warn($"invalid CM size byte (0x{Hex(a_CMBuffer(5))}){vbCrLf}")
                    End If
                    Return False
                End If
            End If

            ' assume valid CM for now (still need to check page IDs)
            g_ValidCM = True

            ' get page info
            a_UnProt = 0
            a_Offset = 36 ' start of the protected table
            While a_Offset < 400
                a_PageID = g_GetWord(a_CMBuffer, a_Offset) And &HFFF
                If a_PageID = &HFFF Then
                    ' EOPT (end of page table)
                    If a_UnProt = 0 Then
                        ' end of protected page table, therefore the offset value is the start of the unprotected page table
                        a_UnProt = 1
                        a_Offset = g_GetWord(a_CMBuffer, a_Offset + 2)
                    Else
                        Exit While ' end of unprotected page table
                    End If
                ElseIf (a_PageID = &HFFC OrElse a_PageID = &HFFE) Then
                    ' Empty or Pad
                    a_Offset += 4
                Else
                    a_Key = a_PageID
                    With CType(g_CM(gtype.page, a_Key), Page)
                        .Version = (a_CMBuffer(a_Offset) >> 4) And &HF
                        .Offset = g_GetWord(a_CMBuffer, a_Offset + 2)
                        .Length = g_GetWord(a_CMBuffer, .Offset + 2)
                        If a_UnProt > 0 Then .Type = Page.TypeDef.unprotected Else .Type = Page.TypeDef.protected
                        ' check that the page headers contain the correct page ID
                        If g_GetWord(a_CMBuffer, .Offset) <> g_GetWord(a_CMBuffer, a_Offset) Then
                            Warn($"CM Page Header Error: Offset = { .Offset} expected {g_GetWord(a_CMBuffer, a_Offset)}")
                        End If
                    End With
                    a_Offset += 4
                End If
            End While
            '===================== Parse the cartridge manufacturers page =====================
            a_Key = 1
            If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, a_Key, False), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 Then
                        a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                        With CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg)
                            .TapeVendor = getstr(a_Buffer, 4, 8)
                            .CartridgeSN = getstr(a_Buffer, 12, 10)
                            .CartridgeType = g_GetWord(a_Buffer, 22)
                            .MfgDate = getstr(a_Buffer, 24, 8) ' format YYYYMMDD
                            .TapeLength = g_GetWord(a_Buffer, 32) ' in .25 metre increments
                            .MediaCode = g_GetWord(a_Buffer, 46)
                            Dim a_PageRevision As Byte = a_Buffer(0)
                            Dim a_Particles As Byte = a_Buffer(42)
                            If a_PageRevision >= &H40 Then
                                If ((a_Particles And &HF) <> 0) Then
                                    .ParticleType = Cartridge_mfg.particle.BaFe
                                Else
                                    .ParticleType = Cartridge_mfg.particle.MP
                                End If
                                If (a_Particles And &HF0) = &H10 Then
                                    .SubstrateType = Cartridge_mfg.substrate.SPALTAN
                                Else
                                    .SubstrateType = Cartridge_mfg.substrate.PEN
                                End If
                            Else
                                If ((a_Buffer(42)) <> 0) Then
                                    .ParticleType = Cartridge_mfg.particle.BaFe
                                Else
                                    .ParticleType = Cartridge_mfg.particle.MP
                                End If
                            End If
                            If (.CartridgeType >> 15 And &H1) = 1 Then
                                .Format = "Cleaning Tape"
                            ElseIf .CartridgeType = 1 Then
                                .Format = "LTO-1"
                                a_NWraps = 48
                                a_SetsPerWrap = 5500
                                a_TapeDirLength = 16
                            ElseIf .CartridgeType = 2 Then
                                .Format = "LTO-2"
                                a_NWraps = 64
                                a_SetsPerWrap = 8200
                                a_TapeDirLength = 28
                            Else 'LTO3+ supports WORM, so need to mask off the 'WORM' bit in the upper byte
                                Select Case .CartridgeType And &HFF
                                    Case 4
                                        .Format = "LTO-3"
                                        a_NWraps = 44
                                        a_SetsPerWrap = 6000
                                        a_TapeDirLength = 32
                                    Case 8
                                        .Format = "LTO-4"
                                        a_NWraps = 56
                                        a_SetsPerWrap = 9500
                                        a_TapeDirLength = 32
                                    Case 16
                                        .Format = "LTO-5"
                                        a_NWraps = 80
                                        a_SetsPerWrap = 7800
                                        a_TapeDirLength = 32
                                    Case 32
                                        .Format = "LTO-6"
                                        a_NWraps = 136
                                        a_SetsPerWrap = 7805
                                        a_TapeDirLength = 32
                                    Case 64
                                        .Format = "LTO-7"
                                        a_NWraps = 112
                                        a_SetsPerWrap = 10950
                                        a_TapeDirLength = 32
                                    Case 128
                                        .Format = "LTO-8"
                                        a_NWraps = 208
                                        a_SetsPerWrap = 11660
                                        a_TapeDirLength = 32
                                    Case 129
                                        .Format = "LTO-9"
                                        a_NWraps = 280
                                        a_SetsPerWrap = 6770
                                        a_TapeDirLength = 32
                                    Case Else
                                        Select Case .CartridgeType
                                            Case 5126
                                                .Format = "3592JA"
                                            Case 13
                                                .Format = "3592JB"
                                            Case 15
                                                .Format = "3592JC"
                                            Case 17
                                                .Format = "3592JD"
                                            Case 19
                                                .Format = "3592JE"
                                            Case 21
                                                .Format = "3592JF"
                                            Case 13318
                                                .Format = "3592JJ"
                                            Case 8207
                                                .Format = "3592JK"
                                            Case 8209
                                                .Format = "3592JL"
                                            Case 8211
                                                .Format = "3592JM"
                                            Case 8213
                                                .Format = "3592JN"

                                        End Select
                                End Select
                                If (.CartridgeType >> 13 And 1) = 1 Then
                                    .Format &= " WORM"
                                End If
                            End If
                        End With
                    End If
                End With
            End If

            '===================== Parse the media manufacturers page =====================
            a_Key = 2
            If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, a_Key, False), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 Then
                        a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                        With CType(g_CM(gtype.media_mfg), Media_mfg)
                            .Version = a_Buffer(0) >> 4
                            Dim suboffset As Integer = 0
                            If .Version >= 8 Then suboffset = 2 'For 3592 CM
                            .MediaMfgDate = getstr(a_Buffer, 4 + suboffset, 8)
                            .MediaVendor = getstr(a_Buffer, 12 + suboffset, 8)
                            ' check the MediaMfgDate for servo band ID
                            Try
                                If CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg).Format.Contains("LTO-8") Then
                                    If .MediaMfgDate.StartsWith("22") Then
                                        CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg).Servo_Band_ID = Cartridge_mfg.svbid.legacy_UDIM
                                    ElseIf .MediaVendor.StartsWith(">>") Then
                                        CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg).Servo_Band_ID = Cartridge_mfg.svbid.non_UDIM
                                    End If
                                End If
                            Catch ex As Exception
                            End Try
                        End With
                    End If
                End With
            End If

            '===================== Parse the usage pages =====================
            ' usage information page definition Is changed For LTO5
            ' -- And the drive serial number field grew longer, so we'll use
            '    a variable For the length rather than assuming 10 chars.
            Dim driveSNlength As Integer
            With CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg)
                If .IsLTO5Plus Then
                    at_Offset = {32, 36, 44, 52, 56, 60, 62, 64, 66, 80}
                    driveSNlength = 16
                Else
                    at_Offset = {24, 28, 36, 44, 48, 52, 54, 56, 58}
                    driveSNlength = 10
                End If
            End With
            ' parse the 4 usage information pages and mechanism related sub-pages
            a_Length = &H40
            If g_CM(gtype.page, &H108, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, &H108, False), Page)
                    If .Length >= 0 Then a_Length = .Length
                End With
            End If
            a_Err = 0
            ' The Mechanism Related Information (Page 106) is vendor specific so we
            ' need To determine which Vendor wrote it before decoding its contents.
            Dim MechRelatedInfoVendorID As String = ""
            If g_CM(gtype.page, &H106, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, &H106), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 Then
                        MechRelatedInfoVendorID = Encoding.ASCII.GetString(substr(a_CMBuffer, .Offset + 4, 8)).TrimEnd
                    End If
                End With
            End If
            For a_Index = 0 To 3
                ' usage info page ID's 0x108-0x10B correspond to usage info pages 1-4 and mechanism related sub-pages 1-4
                a_Key = a_Index + &H108
                If g_CM(gtype.page, a_Key, False) IsNot Nothing AndAlso g_CM(gtype.page, &H106, False) IsNot Nothing Then
                    Dim valid As Boolean = True
                    With CType(g_CM(gtype.page, a_Key, False), Page)
                        If .Offset < 0 OrElse .Length < 0 Then
                            valid = False
                        End If
                    End With
                    With CType(g_CM(gtype.page, &H106, False), Page)
                        If .Offset < 0 OrElse .Length < 0 Then
                            valid = False
                        End If
                    End With
                    If valid Then
                        ' get the usage info page
                        a_Buffer = substr(a_CMBuffer, CType(g_CM(gtype.page, a_Key, False), Page).Offset, a_Length)
                        ' append the corresponding mechanism related sub-page
                        Dim b_Buffer As Byte() = substr(a_CMBuffer, CType(g_CM(gtype.page, &H106, False), Page).Offset + 12 + 64 * a_Index, 64)
                        a_Buffer = a_Buffer.Concat(b_Buffer).ToArray()
                        With a_Usage(a_Index, True)
                            .data0 = a_Buffer
                            .data1 = g_GetDWord(a_Buffer, at_Offset(0))
                        End With
                    Else
                        a_Err = 1
                    End If
                Else
                    a_Err = 1
                End If
            Next

            If a_Err = 0 Then
                'reverse sort the pages by tape thread count
                a_UsageData.Sort(New Comparison(Of UsagePage)(Function(a As UsagePage, b As UsagePage) As Integer
                                                                  Return b.data1.CompareTo(a.data1)
                                                              End Function))
                For i As Integer = 0 To a_UsageData.Count - 1
                    a_UsageData(i).index = i
                Next
                ' parse the sorted data structure and populate the return array
                For a_Index = 0 To 2
                    With CType(g_CM(gtype.usage, a_Index), Usage)
                        .PageID = g_GetWord(a_Usage(a_Index, False).data0, 0)
                        ' parameters need valid data for the current load in order to calculate (previous load fields are set to 0, per CM spec, so difference calculations will be correct)
                        If g_GetWord(a_Usage(a_Index, False).data0, 12) <> 0 Then
                            'Remove trailing whitespace
                            .DrvSN = Encoding.ASCII.GetString(substr(a_Usage(a_Index, False).data0, 12, driveSNlength)).TrimEnd()
                            'IBM-based drives have two leading chars
                            If .DrvSN.Length > 10 Then
                                'Just the last ten chars
                                .DrvSN = .DrvSN.Substring(.DrvSN.Length - 10)
                            End If
                        Else
                            .DrvSN = ""
                        End If
                        .ThreadCount = g_GetDWord(a_Usage(a_Index, False).data0, at_Offset(0))
                        .SetsWritten = g_GetInt64(a_Usage(a_Index, False).data0, at_Offset(1)) - g_GetInt64(a_Usage(a_Index + 1, False).data0, at_Offset(1))
                        .SetsRead = g_GetInt64(a_Usage(a_Index, False).data0, at_Offset(2)) - g_GetInt64(a_Usage(a_Index + 1, False).data0, at_Offset(2))
                        .TotalSets = g_GetInt64(a_Usage(a_Index, False).data0, at_Offset(1)) + g_GetInt64(a_Usage(a_Index, False).data0, at_Offset(2))
                        .WriteRetries = g_GetDWord(a_Usage(a_Index, False).data0, at_Offset(3)) - g_GetDWord(a_Usage(a_Index + 1, False).data0, at_Offset(3))
                        .ReadRetries = g_GetDWord(a_Usage(a_Index, False).data0, at_Offset(4)) - g_GetDWord(a_Usage(a_Index + 1, False).data0, at_Offset(4))
                        .UnRecovWrites = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(5)) - g_GetWord(a_Usage(a_Index + 1, False).data0, at_Offset(5))
                        .UnRecovReads = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(6)) - g_GetWord(a_Usage(a_Index + 1, False).data0, at_Offset(6))
                        .SuspendedWrites = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(7)) - g_GetWord(a_Usage(a_Index + 1, False).data0, at_Offset(7))
                        .FatalSusWrites = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(8)) - g_GetWord(a_Usage(a_Index + 1, False).data0, at_Offset(8))

                        'LTO5 only (doesn't look like early LTO5 drives update this area, so check if there is valid temperature info first)
                        If CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg).IsLTO5Plus AndAlso a_Usage(a_Index, False).data0(76) > 0 Then
                            .SuspendedAppendWrites = g_GetWord(a_Usage(a_Index, False).data0, 28) - g_GetWord(a_Usage(a_Index + 1, False).data0, 28)
                            .LP3Passes = g_GetDWord(a_Usage(a_Index, False).data0, 68) - g_GetDWord(a_Usage(a_Index + 1, False).data0, 68)
                            .MidpointPasses = g_GetDWord(a_Usage(a_Index, False).data0, 72) - g_GetDWord(a_Usage(a_Index + 1, False).data0, 72)
                            .MaxTapeTemp = a_Usage(a_Index, False).data0(76)
                        End If
                        ' Because the code above helpfully (?) appended the mech-related subpage to each usage page,
                        '  we can parse the mech-related data at the same time. But only If it's HP data..
                        If MechRelatedInfoVendorID.Contains("HP") Then
                            .CCQWriteFails = CInt(g_GetInt64(a_Usage(a_Index, False).data0, a_Length) - g_GetInt64(a_Usage(a_Index + 1, False).data0, a_Length))
                            .C2RecovErrors = g_GetDWord(a_Usage(a_Index, False).data0, a_Length + 8) - g_GetDWord(a_Usage(a_Index + 1, False).data0, a_Length + 8)
                            .DirectionChanges = g_GetDWord(a_Usage(a_Index, False).data0, a_Length + 24) - g_GetDWord(a_Usage(a_Index + 1, False).data0, a_Length + 24)
                            .TapePullingTime = g_GetDWord(a_Usage(a_Index, False).data0, a_Length + 28) - g_GetDWord(a_Usage(a_Index + 1, False).data0, a_Length + 28)
                            .TapeMetresPulled = g_GetDWord(a_Usage(a_Index, False).data0, a_Length + 32)
                            .Repositions = g_GetDWord(a_Usage(a_Index, False).data0, a_Length + 36) - g_GetDWord(a_Usage(a_Index + 1, False).data0, a_Length + 36)
                            .TotalLoadUnloads = g_GetDWord(a_Usage(a_Index, False).data0, a_Length + 40)
                            .StreamFails = g_GetDWord(a_Usage(a_Index, False).data0, a_Length + 44) - g_GetDWord(a_Usage(a_Index + 1, False).data0, a_Length + 44)

                            'for some reason, temperature doesn't always get recorded(??)
                            If g_GetWord(a_Usage(a_Index, False).data0, a_Length + 48) > 0 Then
                                .MaxDriveTemp = g_GetWord(a_Usage(a_Index, False).data0, a_Length + 48) / 256
                            End If
                            If g_GetWord(a_Usage(a_Index, False).data0, a_Length + 50) > 0 Then
                                .MinDriveTemp = g_GetWord(a_Usage(a_Index, False).data0, a_Length + 50) / 256
                            End If
                            If .CCQWriteFails < 0 Then .CCQWriteFails = 0
                            If .C2RecovErrors < 0 Then .C2RecovErrors = 0
                            If .DirectionChanges < 0 Then .DirectionChanges = 0
                            If .TapePullingTime < 0 Then .TapePullingTime = 0
                            If .TapeMetresPulled < 0 Then .TapeMetresPulled = 0
                            If .Repositions < 0 Then .Repositions = 0
                            If .StreamFails < 0 Then .StreamFails = 0
                        Else
                            .CCQWriteFails = 0
                            .C2RecovErrors = 0
                        End If
                        .LifeSetsWritten = CInt(g_GetInt64(a_Usage(a_Index, False).data0, at_Offset(1)))
                        .LifeSetsRead = CInt(g_GetInt64(a_Usage(a_Index, False).data0, at_Offset(2)))
                        .LifeWriteRetries = g_GetDWord(a_Usage(a_Index, False).data0, at_Offset(3))
                        .LifeReadRetries = g_GetDWord(a_Usage(a_Index, False).data0, at_Offset(4))
                        .LifeUnRecovWrites = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(5))
                        .LifeUnRecovReads = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(6))
                        .LifeSuspendedWrites = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(7))
                        .LifeFatalSuspWrites = g_GetWord(a_Usage(a_Index, False).data0, at_Offset(8))
                        If at_Offset.Length >= 10 Then .LifeTapeMetresPulled = g_GetDWord(a_Usage(a_Index, False).data0, at_Offset(9))
                        If CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg).IsLTO5Plus Then
                            .LifeSuspAppendWrites = g_GetWord(a_Usage(a_Index, False).data0, 28)
                            .LifeLP3Passes = g_GetDWord(a_Usage(a_Index, False).data0, 68)
                            .LifeMidpointPasses = g_GetDWord(a_Usage(a_Index, False).data0, 72)
                        End If
                        'the following should always be >= 0 (it's not always, as confirmed by looking at the CM buffer (drv fw issue?)
                        If .ThreadCount < 0 Then .ThreadCount = 0
                        If .SetsWritten < 0 Then .SetsWritten = 0
                        If .SetsRead < 0 Then .SetsRead = 0
                        If .WriteRetries < 0 Then .WriteRetries = 0
                        If .ReadRetries < 0 Then .ReadRetries = 0
                        If .UnRecovWrites < 0 Then .UnRecovWrites = 0
                        If .UnRecovReads < 0 Then .UnRecovReads = 0
                        If .SuspendedWrites < 0 Then .SuspendedWrites = 0
                        If .FatalSusWrites < 0 Then .FatalSusWrites = 0
                        If a_Index = 0 Then
                            g_TPM = .TapeMetresPulled
                        End If
                    End With
                Next
            End If

            '===================== Parse the "tape status and tape alert flags" page =====================
            a_Key = &H105
            If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, a_Key, False), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 Then
                        a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                        With CType(g_CM(gtype.status), TapeStatus)
                            .ThreadCount = g_GetDWord(a_Buffer, 12)
                            'check if any encrypted data (LTO4+ only) by checking if the First Encrypted Logical Object field is all 1's
                            If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).IsLTO4Plus Then
                                If g_GetWord(a_Buffer, 22) = &HFFFF AndAlso g_GetWord(a_Buffer, 24) = &HFFFF AndAlso g_GetWord(a_Buffer, 26) = &HFFFF Then
                                    .EncryptedData = False
                                Else
                                    .EncryptedData = True
                                End If
                            End If

                            'for cleaning tapes, bytes 26-27 is the last location used on the tape in quarter-metres
                            If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("Clean") Then
                                .LastLocation = g_GetWord(a_Buffer, 26)
                            End If
                        End With
                    End If


                End With
            End If
            'for cleaning tapes, check if expired
            If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("Clean") Then
                'cleaning length depends on drive type
                If False Then
                    'LTO1
                    a_CleanLength = 18.5
                Else
                    a_CleanLength = 5.5
                End If
                If CType(g_CM(gtype.status, createNew:=False), TapeStatus).LastLocation >= 0 AndAlso CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).TapeLength >= 0 Then
                    a_CleansRemaining = CInt(CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).TapeLength / 4 - 11)
                    a_CleansRemaining = CInt(a_CleansRemaining - CType(g_CM(gtype.status, createNew:=False), TapeStatus).LastLocation / 4)
                    a_CleansRemaining = CInt(a_CleansRemaining / a_CleanLength)
                    If a_CleansRemaining <= 0 Then
                        CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format &= " (expired)"
                    End If
                End If
            End If

            '===================== Parse the initialisation page =====================
            a_Key = &H101
            If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, a_Key, False), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 Then
                        a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                        'initialisation page definition is changed for LTO5+
                        If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).IsLTO5Plus Then
                            at_Offset = {28, 32, 40, 48}
                        Else
                            at_Offset = {22, 24, 32, 40}
                        End If
                        With CType(g_CM(gtype.initialisation), Initialisation)
                            .LP1 = g_GetDWord(a_Buffer, at_Offset(1))
                            .LP3 = g_GetDWord(a_Buffer, at_Offset(2))
                            .LP5 = g_GetDWord(a_Buffer, at_Offset(3))
                        End With
                    End If
                End With
            End If

            '#===================== Parse the EOD page for Partition 0-3 =====================
            For pnum As Integer = 0 To 3
                a_Key = {&H104, &H10E, &H10F, &H110}(pnum)
                If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                    With CType(g_CM(gtype.page, a_Key, False), Page)
                        If .Offset >= 0 AndAlso .Length >= 0 Then
                            a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                            With CType(g_CM(gtype.EOD, pnum), EOD)
                                .Dataset = g_GetDWord(a_Buffer, 24)
                                .WrapNumber = g_GetDWord(a_Buffer, 28)
                                .Validity = g_GetWord(a_Buffer, 32)
                                .PhysicalPosition = g_GetDWord(a_Buffer, 36)
                            End With
                        End If
                    End With
                End If
            Next

            '===================== Parse the cartridge content page (if it exists!) =====================
            If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).IsLTO5Plus Then
                a_Key = &H10D
                If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                    With CType(g_CM(gtype.page, a_Key, False), Page)
                        If .Offset >= 0 AndAlso .Length >= 0 Then
                            a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                            With CType(g_CM(gtype.cartridge_content), CartridgeContent)
                                .Drive_Id = Encoding.ASCII.GetString(substr(a_Buffer, 12, 16)).TrimEnd
                                .Cartridge_Content = g_GetWord(a_Buffer, 28)
                                .PartitionedCartridge = (((a_Buffer(28) >> 3) And 1) <> 0)
                                If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).IsLTO7Plus Then
                                    .Type_M_Cartridge = ((a_Buffer(28) And 1) <> 0)
                                End If
                                If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-5") Then
                                    .Drive_Firmware_Id = Encoding.ASCII.GetString(substr(a_Buffer, 48, 4))
                                Else
                                    .Drive_Firmware_Id = Encoding.ASCII.GetString(substr(a_Buffer, 52, 4))
                                End If
                                If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-7") AndAlso .Type_M_Cartridge Then
                                    CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format = "LTO-7 Type M"
                                    a_NWraps = 168
                                End If
                            End With
                        End If
                    End With
                End If
            End If

            '===================== Parse the Tape Directory page =====================
            a_Key = &H103
            If g_CM(gtype.page, a_Key, False) IsNot Nothing AndAlso g_CM(gtype.EOD, 0, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, a_Key, False), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 AndAlso ((CType(g_CM(gtype.EOD, 0, False), EOD).Validity) <> 0) Then
                        a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                        With CType(g_CM(gtype.tape_directory, createNew:=True), TapeDirectory)
                            .Version = CByte((a_Buffer(0) >> 4) And &HF)
                            If CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).IsLTO6Plus Then
                                a_HdrLength = 48
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 4)
                                .FID_Tape_Write_Pass_Partition_0 = a_TapeWritePassPartition
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 8)
                                .FID_Tape_Write_Pass_Partition_1 = a_TapeWritePassPartition
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 12)
                                .FID_Tape_Write_Pass_Partition_2 = a_TapeWritePassPartition
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 16)
                                .FID_Tape_Write_Pass_Partition_3 = a_TapeWritePassPartition
                                a_OutputStr = $"{"WritePass".PadRight(12) _
                                    }{"DatasetID".PadRight(14) _
                                    }{"HOW RecCnt".PadRight(14) _
                                    }{"EOW RecCnt".PadRight(14) _
                                    }{"HOW FMCnt".PadRight(14) _
                                    }{"EOW FMCnt".PadRight(14) _
                                    }{"FM Map".PadRight(14) _
                                    }{"CRC".PadRight(14)}"
                                .Wrap = a_OutputStr
                            ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).IsLTO4Plus Then
                                a_HdrLength = 16
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 4)
                                .FID_Tape_Write_Pass_Partition_0 = a_TapeWritePassPartition
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 8)
                                .FID_Tape_Write_Pass_Partition_1 = a_TapeWritePassPartition
                                a_OutputStr = $"{"WritePass".PadRight(12) _
                                    }{"DatasetID".PadRight(14) _
                                    }{"HOW RecCnt".PadRight(14) _
                                    }{"EOW RecCnt".PadRight(14) _
                                    }{"HOW FMCnt".PadRight(14) _
                                    }{"EOW FMCnt".PadRight(14) _
                                    }{"FM Map".PadRight(14) _
                                    }{"CRC".PadRight(14)}"
                                .Wrap = a_OutputStr
                            ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-3") Then
                                a_HdrLength = 16
                                a_OutputStr = $"{"WritePass".PadRight(12) _
                                    }{"DatasetID".PadRight(14) _
                                    }{"HOW RecCnt".PadRight(14) _
                                    }{"EOW RecCnt".PadRight(14) _
                                    }{"HOW FMCnt".PadRight(14) _
                                    }{"EOW FMCnt".PadRight(14) _
                                    }{"FM Map".PadRight(14) _
                                    }{"CRC".PadRight(14)}"
                                .Wrap = a_OutputStr
                            ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-2") Then
                                a_HdrLength = 16
                                a_OutputStr = $"{"WritePass".PadRight(12) _
                                    }{"DatasetID".PadRight(14) _
                                    }{"HOW RecCnt".PadRight(14) _
                                    }{"EOW RecCnt".PadRight(14) _
                                    }{"HOW FMCnt".PadRight(14) _
                                    }{"EOW FMCnt".PadRight(14) _
                                    }{"CRC".PadRight(14)}"
                                .Wrap = a_OutputStr
                            ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-1") Then
                                a_HdrLength = 16
                                a_OutputStr = $"{"WritePass".PadRight(12) _
                                    }{"DatasetID".PadRight(14) _
                                    }{"RecordCount".PadRight(14) _
                                    }{"FilemarkCount".PadRight(14) _
                                    }{"CRC".PadRight(14)}"
                                .Wrap = a_OutputStr
                            ElseIf .Version = 9 Then
                                a_HdrLength = 16
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 4)
                                .FID_Tape_Write_Pass_Partition_0 = a_TapeWritePassPartition
                                a_TapeWritePassPartition = g_GetDWord(a_Buffer, 8)
                                .FID_Tape_Write_Pass_Partition_1 = a_TapeWritePassPartition
                                a_OutputStr = $"{"WritePass".PadRight(12) _
                                    }{"DatasetID".PadRight(14) _
                                    }{"HOW RecCnt".PadRight(14) _
                                    }{"EOW RecCnt".PadRight(14) _
                                    }{"HOW FMCnt".PadRight(14) _
                                    }{"EOW FMCnt".PadRight(14) _
                                    }{"FM Map".PadRight(14)}"
                                .Wrap = a_OutputStr
                            Else
                                a_HdrLength = 16
                            End If
                            PublishTapeDirectoryPage(a_Buffer)
                            a_LastID = 0

                            ' Get number of partitions.

                            a_NumPartitions = New List(Of EOD)
                            For Each eod As EOD In PartitionEOD
                                a_NumPartitions.Add(eod)
                            Next
                            .CapacityLoss = New List(Of Double)
                            For a_Index = 0 To a_NWraps - 1
                                a_SetID = g_GetDWord(a_Buffer, a_HdrLength + a_TapeDirLength * a_Index + 4)
                                If a_SetID = UNUSED_WRAP_IDENTIFIER Then
                                    .CapacityLoss.Add(-1)
                                    Continue For
                                ElseIf a_SetID = GUARD_WRAP_IDENTIFIER Then
                                    .CapacityLoss.Add(-3)
                                    Continue For
                                ElseIf a_SetID = 0 Then
                                    .CapacityLoss.Add(0)
                                    Continue For
                                Else
                                    a_set = 0
                                    For a_PartitionKey = 0 To a_NumPartitions.Count - 1
                                        If ((CType(g_CM(gtype.EOD, a_PartitionKey, False), EOD).Validity) <> 0) AndAlso CType(g_CM(gtype.EOD, a_PartitionKey, False), EOD).WrapNumber = a_Index Then
                                            .CapacityLoss.Add(-2)
                                            'partially written wrap (EOD Wrap).
                                            a_set = 1
                                            Exit For
                                        End If
                                    Next
                                    If a_set = 0 Then
                                        .CapacityLoss.Add(Math.Max(0, 100 * (1 - (a_SetID - a_LastID) / a_SetsPerWrap)))
                                        a_LastID = a_SetID
                                    End If
                                End If
                            Next
                            a_LastID = 0
                            Dim a As Integer
                            Dim DatasetsOnWrap_Index As Integer
                            Dim TapeDirectoryEntry As Byte()
                            For a_Index = 0 To a_NWraps - 1
                                DatasetsOnWrap_Index = a_Index
                                TapeDirectoryEntry = substr(a_Buffer, a_HdrLength + a_TapeDirLength * a_Index, a_TapeDirLength)
                                a = g_GetDWord(TapeDirectoryEntry, 4)
                                If a = UNUSED_WRAP_IDENTIFIER OrElse a = GUARD_WRAP_IDENTIFIER Then
                                    a = 0
                                    .DatasetsOnWrap(DatasetsOnWrap_Index).Data = a
                                Else
                                    .DatasetsOnWrap(DatasetsOnWrap_Index).Data = a - a_LastID
                                End If
                                a_LastID = a
                            Next
                        End With
                    End If
                End With
            End If

            'if partitioned cart and LTO5 or later
            If CType(g_CM(gtype.cartridge_content), CartridgeContent).PartitionedCartridge Then
                Dim pKeyList() As Integer = {}
                If CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg).Format.Contains("LTO-5") Then
                    pKeyList = {&H10E}
                ElseIf CType(g_CM(gtype.cartridge_mfg), Cartridge_mfg).IsLTO6Plus Then
                    pKeyList = {&H10E, &H10F, &H110}
                End If
                For pNum As Integer = 1 To pKeyList.Count
                    a_Key = pKeyList(pNum - 1)
                    If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                        With CType(g_CM(gtype.page, a_Key, False), Page)
                            If .Length >= 0 AndAlso .Offset >= 0 Then
                                a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                                With CType(g_CM(gtype.EOD, pNum), EOD)
                                    .Dataset = g_GetDWord(a_Buffer, 24)
                                    .WrapNumber = g_GetDWord(a_Buffer, 28)
                                    .Validity = g_GetWord(a_Buffer, 32)
                                    .PhysicalPosition = g_GetDWord(a_Buffer, 36)
                                End With
                            End If
                        End With
                    End If
                Next
            End If

            '===================== Parse the Suspended Append Writes page =====================
            a_Key = &H107
            Dim a_NoSlot As Integer
            a_Index = 0
            a_Offset = 0
            If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, a_Key, False), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 Then
                        a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                        If Not CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).IsLTO5Plus Then
                            ' LTO-1 through LTO-4 tapes have 14 slots (0-13)
                            a_NoSlot = 14
                        ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-5") Then
                            'LTO-5 tape has 22 slots (0-21)
                            a_NoSlot = 22
                        ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-6") Then
                            'LTO-6 tape has 38 slots (0-37)
                            a_NoSlot = 38
                        ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-7") Then
                            'LTO-7 tape has 38 slots (0-37)
                            a_NoSlot = 38
                        ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-8") Then
                            'LTO-8 tape has 38 slots (0-37), including Type M
                            a_NoSlot = 38
                        ElseIf CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg).Format.Contains("LTO-9") Then
                            'LTO-9 tape has 38 slots (0-37)
                            a_NoSlot = 38
                        End If

                        'Single partition, look for available slots, index 0 - 13
                        For a_Index = 0 To a_NoSlot - 1
                            With CType(g_CM(gtype.suspended_writes), SuspendWrite)
                                .DataSetID(a_Index).Value = g_GetDWord(a_Buffer, a_Offset + 8)
                                .WTapePass(a_Index).Value = g_GetDWord(a_Buffer, a_Offset + 12)
                                a_Offset += 8
                            End With
                        Next
                    End If
                End With
            End If

            '===================== Parse the Tape Control page (LTO9 Only)=====================
            'a_Key = &H183
            'tapectrl
            '    TDS Data Version           BYTE        a_Buffer(4)
            '    Temperature                BYTE        a_Buffer(5)
            '    Humidity                   BYTE        a_Buffer(6)
            '    Characterization needed    BYTE        a_Buffer(7)
            '    Timestamp                  DWORD       a_Buffer(16)
            '    DriveSN                    Byte(12)    a_Buffer(34)

            '===================== Parse the Application Specific page =====================
            ' See MAMAttribute
            a_Key = &H200
            If g_CM(gtype.page, a_Key, False) IsNot Nothing Then
                With CType(g_CM(gtype.page, a_Key, False), Page)
                    If .Offset >= 0 AndAlso .Length >= 0 Then
                        a_Buffer = substr(a_CMBuffer, .Offset, .Length)
                        With CType(g_CM(gtype.application_specific), ApplicationSpecific)
                            If getstr(a_Buffer, 4, 6).Equals("MAM001") OrElse getstr(a_Buffer, 4, 6).Equals("MAM002") Then
                                a_Index = 10
                                While a_Index < a_Buffer.Length
                                    a_AttributeID = g_GetWord(a_Buffer, a_Index)
                                    ' Attribute length definition changed from "MAM001" To "MAM002".  "MAM001" has 2 bytes For attribute length, whereas
                                    ' "MAM002" has 1.5 bytes.  Using 1.5 bytes For both cases, As the attribute length Is always small compared To what
                                    ' can be represented by 1.5 bytes (QXCR1001109840).
                                    a_AttributeLength = g_GetWord(a_Buffer, a_Index + 2) And &HFFF
                                    If a_AttributeID = &HFFF OrElse a_AttributeLength = 0 Then Exit While
                                    ' barcode
                                    If a_AttributeID = &H806 Then
                                        a_Barcode = getstr(a_Buffer, a_Index + 4, a_AttributeLength).TrimEnd()
                                        If a_Barcode <> "" Then
                                            .Barcode = a_Barcode
                                            g_Barcode = a_Barcode
                                        End If
                                    End If

                                    ' LTFS vendor
                                    If a_AttributeID = &H800 Then
                                        .Application_vendor = getstr(a_Buffer, a_Index + 4, a_AttributeLength).TrimEnd()
                                    End If

                                    ' LTFS formatted
                                    If a_AttributeID = &H801 Then
                                        .Application_name = getstr(a_Buffer, a_Index + 4, a_AttributeLength).TrimEnd()
                                    End If

                                    ' LTFS version
                                    If a_AttributeID = &H802 Then
                                        .Application_version = getstr(a_Buffer, a_Index + 4, a_AttributeLength).TrimEnd()
                                    End If

                                    a_Index += 4 + a_AttributeLength
                                End While
                            End If
                        End With
                    End If
                End With
            End If
            Return True
        End Function
        Public Sub PublishTapeDirectoryPage(ByRef Buffer As Byte())
            Dim a_WrapIndex As Integer = 0
            Dim a_WritePass As Integer = 0
            Dim a_DataSetID As Integer = 0
            Dim a_HOW_RecCnt As Integer = 0
            Dim a_EOW_RecCnt As Integer = 0
            Dim a_HOW_FMCnt As Integer = 0
            Dim a_EOW_FMCnt As Integer = 0
            Dim a_FM_MAP As Integer = 0
            Dim a_CRC As Integer = 0
            Dim a_WrapsInDrive As Integer = 0
            Dim a_RecordCount As Integer = 0
            Dim a_FilemarkCount As Integer = 0

            Dim a_EvenDataSetID As Integer = 0
            Dim a_EvenRecordCount As Integer = 0
            Dim a_EvenFileMarkCount As Integer = 0
            Dim a_EvenCRC As Integer = 0
            Dim a_OddDataSetID As Integer = 0
            Dim a_OddRecordCount As Integer = 0
            Dim a_OddFileMarkCount As Integer = 0
            Dim a_OddCRC As Integer = 0
            Dim a_HdrLength As Integer = 0
            Dim a_OutputStr As String = ""

            With CType(g_CM(gtype.cartridge_mfg, createNew:=False), Cartridge_mfg)
                If .Format.Contains("LTO-1") Then
                    a_WrapsInDrive = 48
                    a_HdrLength = 16
                ElseIf .Format.Contains("LTO-2") Then
                    a_WrapsInDrive = 64
                    a_HdrLength = 16
                ElseIf .Format.Contains("LTO-3") Then
                    a_WrapsInDrive = 44
                    a_HdrLength = 16
                ElseIf .Format.Contains("LTO-4") Then
                    a_WrapsInDrive = 56
                    a_HdrLength = 16
                ElseIf .Format.Contains("LTO-5") Then
                    a_WrapsInDrive = 80
                    a_HdrLength = 16
                ElseIf .Format.Contains("LTO-6") Then
                    a_WrapsInDrive = 136
                    a_HdrLength = 48
                ElseIf .Format.Contains("LTO-7") Then
                    If .Format.Contains("Type M") Then
                        a_WrapsInDrive = 168
                    Else
                        a_WrapsInDrive = 112
                    End If
                    a_HdrLength = 48
                ElseIf .Format.Contains("LTO-8") Then
                    a_WrapsInDrive = 208
                    a_HdrLength = 48
                ElseIf .Format.Contains("LTO-9") Then
                    a_WrapsInDrive = 280
                    a_HdrLength = 48
                ElseIf .Format.Contains("J") Then
                    a_HdrLength = 16
                    a_WrapsInDrive = ((Buffer.Length - a_HdrLength) \ (24 * 8 + 4)) * 8
                    a_NWraps = a_WrapsInDrive
                End If

                If .Format.Contains("LTO-2") Then
                    For a_WrapIndex = 0 To a_WrapsInDrive - 1
                        a_WritePass = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_DataSetID = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_HOW_RecCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_EOW_RecCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_HOW_FMCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_EOW_FMCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_CRC = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_OutputStr = $"{a_WritePass.ToString().PadRight(12) _
                            }{a_DataSetID.ToString().PadRight(12) _
                            }{a_HOW_RecCnt.ToString().PadRight(12) _
                            }{a_EOW_RecCnt.ToString().PadRight(12) _
                            }{a_HOW_FMCnt.ToString().PadRight(12) _
                            }{a_EOW_FMCnt.ToString().PadRight(12) _
                            }{a_CRC.ToString().PadRight(12) _
                            }"

                        With CType(g_CM(gtype.tape_directory), TapeDirectory).WrapEntry(a_WrapIndex)
                            .Content = a_OutputStr
                            .RawData = {a_DataSetID, a_HOW_RecCnt, a_EOW_RecCnt, a_HOW_FMCnt, a_EOW_FMCnt, a_CRC}
                            .RecCount = a_HOW_RecCnt + a_EOW_RecCnt
                            .FileMarkCount = a_HOW_FMCnt + a_EOW_FMCnt
                        End With
                    Next
                ElseIf .IsLTO3Plus Then
                    For a_WrapIndex = 0 To a_WrapsInDrive - 1
                        a_WritePass = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_DataSetID = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_HOW_RecCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_EOW_RecCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_HOW_FMCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_EOW_FMCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_FM_MAP = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_CRC = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_OutputStr = $"{a_WritePass.ToString().PadRight(12) _
                            }{a_DataSetID.ToString().PadRight(12) _
                            }{a_HOW_RecCnt.ToString().PadRight(12) _
                            }{a_EOW_RecCnt.ToString().PadRight(12) _
                            }{a_HOW_FMCnt.ToString().PadRight(12) _
                            }{a_EOW_FMCnt.ToString().PadRight(12) _
                            }{a_FM_MAP.ToString().PadRight(12) _
                            }{a_CRC.ToString().PadRight(12) _
                            }"
                        With CType(g_CM(gtype.tape_directory), TapeDirectory).WrapEntry(a_WrapIndex)
                            .Content = a_OutputStr
                            .RawData = {a_DataSetID, a_HOW_RecCnt, a_EOW_RecCnt, a_HOW_FMCnt, a_EOW_FMCnt, a_FM_MAP, a_CRC}
                            .RecCount = a_HOW_RecCnt + a_EOW_RecCnt
                            .FileMarkCount = a_HOW_FMCnt + a_EOW_FMCnt
                        End With
                    Next
                ElseIf .Format.Contains("LTO-1") Then
                    a_HdrLength = 16
                    a_WrapsInDrive = 48
                    For a_WrapIndex = 0 To a_WrapsInDrive - 1
                        a_EvenDataSetID = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_EvenRecordCount = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_EvenFileMarkCount = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_EvenCRC = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_OddDataSetID = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_OddRecordCount = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_OddFileMarkCount = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_OddCRC = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4

                        a_OutputStr = $"{a_EvenDataSetID.ToString().PadRight(12) _
                            }{a_EvenRecordCount.ToString().PadRight(12) _
                            }{a_EvenFileMarkCount.ToString().PadRight(12) _
                            }{a_EvenCRC.ToString().PadRight(12) _
                            }{a_OddDataSetID.ToString().PadRight(12) _
                            }{a_OddRecordCount.ToString().PadRight(12) _
                            }{a_OddFileMarkCount.ToString().PadRight(12) _
                            }{a_OddCRC.ToString().PadRight(12) _
                            }"

                        With CType(g_CM(gtype.tape_directory), TapeDirectory).WrapEntry(a_WrapIndex)
                            .Content = a_OutputStr
                            .RawData = {a_EvenDataSetID, a_EvenRecordCount, a_EvenFileMarkCount, a_EvenCRC, a_OddDataSetID, a_OddRecordCount, a_OddFileMarkCount, a_OddCRC}
                            .RecCount = a_EvenRecordCount + a_OddRecordCount
                            .FileMarkCount = a_EvenFileMarkCount + a_OddFileMarkCount
                        End With
                    Next
                ElseIf .Format.Contains("J") Then
                    For a_WrapIndex = 0 To a_WrapsInDrive - 1
                        a_WritePass = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_DataSetID = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        'a_HOW_RecCnt = g_GetDWord(Buffer, a_HdrLength)
                        'a_HdrLength += 4
                        a_EOW_RecCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        'a_HOW_FMCnt = g_GetDWord(Buffer, a_HdrLength)
                        'a_HdrLength += 4
                        a_EOW_FMCnt = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 4
                        a_FM_MAP = g_GetDWord(Buffer, a_HdrLength)
                        a_HdrLength += 8
                        If a_WrapIndex Mod 8 = 7 Then
                            a_CRC = g_GetDWord(Buffer, a_HdrLength)
                            a_HdrLength += 4
                        End If
                        a_OutputStr = $"{a_WritePass.ToString().PadRight(12) _
                            }{a_DataSetID.ToString().PadRight(12) _
                            }{a_HOW_RecCnt.ToString().PadRight(12) _
                            }{a_EOW_RecCnt.ToString().PadRight(12) _
                            }{a_HOW_FMCnt.ToString().PadRight(12) _
                            }{a_EOW_FMCnt.ToString().PadRight(12) _
                            }{a_FM_MAP.ToString().PadRight(12) _
                            }"
                        With CType(g_CM(gtype.tape_directory), TapeDirectory).WrapEntry(a_WrapIndex)
                            .Content = a_OutputStr
                            .RawData = {a_DataSetID, a_HOW_RecCnt, a_EOW_RecCnt, a_HOW_FMCnt, a_EOW_FMCnt, a_FM_MAP, a_CRC}
                            .RecCount = a_HOW_RecCnt + a_EOW_RecCnt
                            .FileMarkCount = a_HOW_FMCnt + a_EOW_FMCnt
                        End With
                    Next
                End If
            End With

        End Sub
        Public Function g_GetWord(buffer As Byte(), offset As Integer) As Integer
            Return CInt(buffer(offset)) << 8 Or buffer(offset + 1)
        End Function
        Public Function g_GetDWord(buffer As Byte(), offset As Integer) As Integer
            Dim result As Integer
            For i As Integer = 0 To 3
                result <<= 8
                result = result Or buffer(offset + i)
            Next
            Return result
        End Function
        Public Function g_GetInt64(buffer As Byte(), offset As Integer) As Long
            Dim result As Long
            For i As Integer = 0 To 7
                result <<= 8
                result = result Or buffer(offset + i)
            Next
            Return result
        End Function
        Public Function substr(buffer As Byte(), offset As Long, length As Long) As Byte()
            Dim result(CInt(length - 1)) As Byte
            Array.Copy(buffer, offset, result, 0, length)
            Return result
        End Function
        Public Function getstr(buffer As Byte(), offset As Long, length As Long) As String
            Dim result(CInt(length - 1)) As Byte
            Array.Copy(buffer, offset, result, 0, length)
            Return Encoding.ASCII.GetString(result)
        End Function
        Public Sub New()

        End Sub
        Public Sub New(ByVal BufferData As Byte(), Optional ByRef errorMsg As Exception = Nothing)
            a_CMBuffer = BufferData
            If a_CMBuffer.Length <> 0 Then
                Try
                    RunParse()
                Catch ex As Exception
                    errorMsg = ex
                End Try
            End If
        End Sub
        Public Function GetReport() As String
            Dim Output As New StringBuilder
            Output.Append("+=========================== APPLICATION INFO ============================+" & vbCrLf)
            Try
                Dim BC As String = ApplicationSpecificData.Barcode
                'BC = TapeUtils.ReadBarcode(ConfTapeDrive)
                'BC = TapeUtils.ReadBarcode(ConfTapeDrive)
                Output.Append(("| Barcode: ".PadRight(28) & BC).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Barcode: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Try
                Dim AppInfo As String = $"{ApplicationSpecificData.Application_vendor} {ApplicationSpecificData.Application_name} {ApplicationSpecificData.Application_version}" ' TapeUtils.ReadAppInfo(ConfTapeDrive)
                Output.Append(("| Application: ".PadRight(28) & AppInfo).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Application: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Output.Append("+============================= MEDIUM USAGE ==============================+" & vbCrLf)
            If CartridgeMfgData.CartridgeTypeAbbr = "CU" Then
                Try
                    Dim LoadCount As Int64 = StatusData.ThreadCount ' TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 0, 3).AsNumeric
                    Output.Append(("| Cleans performed: ".PadRight(28) & LoadCount).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Cleans performed: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Dim CleanRemain As Int64 = a_CleansRemaining ' TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 0, 3).AsNumeric
                    Output.Append(("| Cleans remain: ".PadRight(28) & CleanRemain).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Cleans remain: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Dim TapeLen As Int64 = CartridgeMfgData.TapeLength ' TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 0, 3).AsNumeric
                    Dim LastLoc As Int64 = StatusData.LastLocation ' TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 0, 3).AsNumeric
                    Output.Append(("| Used length: ".PadRight(28) & $"{(LastLoc / 4).ToString("f2")} m / {((TapeLen / 4) - 11).ToString("f2")} m").PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Used length: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try

            Else
                Try
                    Dim LoadCount As Int64 = StatusData.ThreadCount ' TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 0, 3).AsNumeric
                    Output.Append(("| Load count: ".PadRight(28) & LoadCount).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Load count: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Dim TotalWriteKBytes As Int64 = CartridgeMfgData.KB_PER_DATASET
                    TotalWriteKBytes *= UsageData(0).LifeSetsWritten
                    If CartridgeMfgData.KB_PER_DATASET > 0 Then
                        Output.Append(("| Total write: ".PadRight(28) & ReduceDataUnit(TotalWriteKBytes)).PadRight(74) & "|" & vbCrLf)
                    Else
                        Output.Append(("| Total write: ".PadRight(28) & $"{UsageData(0).LifeSetsWritten} Sets").PadRight(74) & "|" & vbCrLf)
                    End If
                Catch ex As Exception
                    Output.Append(("| Total write: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Dim TotalReadKBytes As Int64 = CartridgeMfgData.KB_PER_DATASET
                    TotalReadKBytes *= UsageData(0).LifeSetsRead
                    If CartridgeMfgData.KB_PER_DATASET > 0 Then
                        Output.Append(("| Total read: ".PadRight(28) & ReduceDataUnit(TotalReadKBytes)).PadRight(74) & "|" & vbCrLf)
                    Else
                        Output.Append(("| Total read: ".PadRight(28) & $"{UsageData(0).LifeSetsRead} Sets").PadRight(74) & "|" & vbCrLf)
                    End If
                Catch ex As Exception
                    Output.Append(("| Total read: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Dim fve As Double = (UsageData(0).LifeSetsRead + UsageData(0).LifeSetsWritten) / (a_SetsPerWrap * a_NWraps)
                    If a_SetsPerWrap > 0 Then
                        Output.Append(("| Full volume equivalents: ".PadRight(28) & fve.ToString("f2") & $" FVE ({(fve / CartridgeMfgData.TAPE_LIFE_IN_VOLS * 100).ToString("f2")}%)").PadRight(74) & "|" & vbCrLf)
                    Else
                        Output.Append(("| Full volume equivalents: ".PadRight(28) & $"Unknown").PadRight(74) & "|" & vbCrLf)
                    End If
                Catch ex As Exception
                    Output.Append(("| Full volume equivalents: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Output.Append(("| Write retries: ".PadRight(28) & UsageData(0).LifeWriteRetries).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Write retries: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Output.Append(("| Read retries: ".PadRight(28) & UsageData(0).LifeReadRetries).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Read retries: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Output.Append(("| Unrecovered writes: ".PadRight(28) & UsageData(0).LifeUnRecovWrites).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Unrecovered writes: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Output.Append(("| Unrecovered reads: ".PadRight(28) & UsageData(0).LifeUnRecovReads).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Unrecovered reads: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Output.Append(("| Suspended writes: ".PadRight(28) & UsageData(0).LifeSuspendedWrites).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Suspended writes: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Output.Append(("| Suspended append writes: ".PadRight(28) & UsageData(0).LifeSuspAppendWrites).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Suspended append writes: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Output.Append(("| Fatal suspended writes: ".PadRight(28) & UsageData(0).LifeFatalSuspWrites).PadRight(74) & "|" & vbCrLf)
                Catch ex As Exception
                    Output.Append(("| Fatal suspended writes: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
                End Try
            End If

            'Try
            '    Dim TapeMetresPulled As Int64 = Me.UsageData(0).LifeTapeMetresPulled
            '    If TapeMetresPulled <= 0 Then Throw New Exception("TMP Not available")
            '    TextBox8.Append(" Tape pulled: ".PadRight(28) & TapeMetresPulled & " m" & vbCrLf)
            'Catch ex As Exception
            '    TextBox8.Append(" Tape pulled: ".PadRight(28) & "Not available" & vbCrLf)
            'End Try
            Output.Append("+============================ MEDIUM IDENTITY ============================+" & vbCrLf)
            Try
                Dim Medium_Format As String = $"{CartridgeMfgData.Format} (MC 0x{CartridgeMfgData.MediaCode.ToString("X4")} DC 0x{CartridgeMfgData.DENSITY_CODE.ToString("X2")})"
                Output.Append(("| Format: ".PadRight(28) & Medium_Format).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Format: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Try
                Dim Medium_SN As String = CartridgeMfgData.CartridgeSN ' TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 4, 1).AsString
                Output.Append(("| Serial number: ".PadRight(28) & Medium_SN).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Serial number: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Try
                Dim Tape_Manufacturer As String = CartridgeMfgData.TapeVendor 'TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 4, 0).AsString
                Output.Append(("| Tape Vendor: ".PadRight(28) & Tape_Manufacturer).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Tape Vendor: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Try
                Dim Tape_Man_Date As String = CartridgeMfgData.MfgDate 'TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 4, 6).AsString
                Output.Append(("| Tape mfg date: ".PadRight(28) & Tape_Man_Date).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Tape mfg date: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Try
                Dim Medium_Manufacturer As String = MediaMfgData.MediaVendor 'TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 4, 0).AsString
                Output.Append(("| Media Vendor: ".PadRight(28) & Medium_Manufacturer).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Media Vendor: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Try
                Dim Medium_Man_Date As String = MediaMfgData.MediaMfgDate 'TapeUtils.MAMAttribute.FromTapeDrive(ConfTapeDrive, 4, 6).AsString
                Output.Append(("| Media mfg date: ".PadRight(28) & Medium_Man_Date).PadRight(74) & "|" & vbCrLf)
            Catch ex As Exception
                Output.Append(("| Media mfg date: ".PadRight(28) & "Not available").PadRight(74) & "|" & vbCrLf)
            End Try
            Try
                Dim CMData As Byte() = a_CMBuffer
                Dim Medium_ParticleType As String = CartridgeMfgData.ParticleType.ToString()
                If CartridgeMfgData.CartridgeTypeAbbr = "CU" Then Medium_ParticleType = "Universal Clean Cartridge"
                Output.Append(("| Particle type: ".PadRight(28) & Medium_ParticleType).PadRight(74) & "|" & vbCrLf)
                Output.Append("+============================= DATA ON TAPE ==============================+" & vbCrLf)
                Dim wares As New StringBuilder
                Dim nLossDS As Long = 0
                Dim DataSize As New List(Of Long)
                Try
                    If CartridgeMfgData.CartridgeTypeAbbr = "CU" Then Exit Try
                    wares.AppendLine("+============================= WRAP ANALYSIS =============================+")
                    wares.AppendLine("| Wrap | Start Block |  End Block  | Filemark |      Set      | Capacity  |")
                    wares.AppendLine("|------+-------------+-------------+----------+---------------+-----------|")
                    Dim StartBlock As Integer = 0
                    Dim CurrSize As Long = 0
                    Dim gw As Boolean = False
                    For wn As Integer = 0 To a_NWraps - 1
                        Dim StartBlockStr As String = StartBlock.ToString()
                        If TapeDirectoryData.CapacityLoss(wn) = -1 Or TapeDirectoryData.CapacityLoss(wn) = -3 Then StartBlockStr = ""
                        Dim EndBlock As Integer = StartBlock + TapeDirectoryData.WrapEntryInfo(wn).RecCount + TapeDirectoryData.WrapEntryInfo(wn).FileMarkCount - 1
                        If TapeDirectoryData.CapacityLoss(wn) = -2 Then EndBlock += 1
                        wares.Append($"| {wn.ToString().PadLeft(3)}  |")
                        wares.Append($" {StartBlockStr.PadLeft(10)}  |")
                        If StartBlockStr <> "" Then
                            wares.Append($"  {EndBlock.ToString.PadLeft(10)} |")
                        Else
                            wares.Append($"  {"".PadLeft(10)} |")
                        End If
                        wares.Append($"  {TapeDirectoryData.WrapEntryInfo(wn).FileMarkCount.ToString().PadLeft(5)}   |")
                        wares.Append($" {TapeDirectoryData.DatasetsOnWrapData(wn).Data.ToString().PadLeft(5)} / {a_SetsPerWrap.ToString().PadRight(5)} |")
                        StartBlock += TapeDirectoryData.WrapEntryInfo(wn).RecCount + TapeDirectoryData.WrapEntryInfo(wn).FileMarkCount
                        If TapeDirectoryData.CapacityLoss(wn) >= 0 Then
                            nLossDS += Math.Max(0, a_SetsPerWrap - TapeDirectoryData.DatasetsOnWrapData(wn).Data)
                            CurrSize += TapeDirectoryData.DatasetsOnWrapData(wn).Data
                            'wares.Append($" { (100 - Me.TapeDirectoryData.CapacityLoss(wn)).ToString("f2").PadLeft(7)}%  |")
                            wares.Append($" { (TapeDirectoryData.DatasetsOnWrapData(wn).Data / a_SetsPerWrap * 100).ToString("f2").PadLeft(7)}%  |")
                        ElseIf TapeDirectoryData.CapacityLoss(wn) = -1 Then
                            StartBlock = 0
                            wares.Append($"           |")
                        ElseIf TapeDirectoryData.CapacityLoss(wn) = -2 Then
                            CurrSize += TapeDirectoryData.DatasetsOnWrapData(wn).Data
                            wares.Append($"  >>EOD<<  |")
                        ElseIf TapeDirectoryData.CapacityLoss(wn) = -3 Then
                            StartBlock = 0
                            If gw Then
                                DataSize.Add(CurrSize)
                                CurrSize = 0
                                gw = False
                            Else
                                gw = True
                            End If
                            wares.Append($"  *GUARD*  |")
                        End If
                        wares.AppendLine()
                    Next
                    DataSize.Add(CurrSize)
                Catch ex As Exception
                    wares.Append("| CM data parsing failed.".PadRight(74) & "|" & vbCrLf)
                End Try
                Try
                    Dim DataWrapList As New List(Of Integer)
                    Dim DataWrapNum As Integer = 0
                    For Each l As Double In TapeDirectoryData.CapacityLoss
                        If l = -3 Then
                            If DataWrapNum > 0 Then
                                DataWrapList.Add(DataWrapNum)
                                DataWrapNum = 0
                            End If
                        Else
                            DataWrapNum += 1
                        End If
                    Next
                    If DataWrapNum > 0 Then DataWrapList.Add(DataWrapNum)
                    Output.Append(("| Total partitions: ".PadRight(28) & DataWrapList.Count).PadRight(74) & "|" & vbCrLf)
                    For i As Integer = 0 To DataWrapList.Count - 1
                        Dim nWrap As Long = DataWrapList(i)
                        Dim len As Long = nWrap * CartridgeMfgData.KB_PER_WRAP

                        Dim WrittenSize As String = ""
                        If DataSize.Count = DataWrapList.Count Then
                            WrittenSize = $"{IOManager.FormatSize(DataSize(i) * CartridgeMfgData.KB_PER_DATASET * 1000, True)} / "
                        End If
                        Output.Append(($"| Partition {i} size: ".PadRight(28) & (WrittenSize & ReduceDataUnit(len)).PadRight(24) & $"[{nWrap.ToString().PadLeft(3)} wraps]").PadRight(74) & "|" & vbCrLf)
                    Next
                Catch ex As Exception
                    Output.Append("Partition page not available" & vbCrLf)
                End Try
                Output.Append(("| Estimated capacity loss: ".PadRight(28) & IOManager.FormatSize(nLossDS * CartridgeMfgData.KB_PER_DATASET * 1000)).PadRight(74) & "|" & vbCrLf)
                Output.Append(wares.ToString())

                Output.Append("+============================== CM RAW DATA ==============================+" & vbCrLf)
                Output.Append(("| Length: " & CMData.Length).PadRight(74) & "|" & vbCrLf)
                Output.Append(RichByte2Hex(CMData, True))
                Output.Append("+=========================================================================+")
                Output.Append(vbCrLf)
            Catch ex As Exception
                Output.Append("| CM data parsing failed.".PadRight(74) & "|" & vbCrLf & ex.ToString & vbCrLf)
            End Try
            Return Output.ToString().Replace(vbNullChar, " ")
        End Function
    End Class
    Public Class IOManager
        Public Shared Function FormatSize(byteCount As Long, Optional ByVal More As Boolean = False) As String
            Dim sym As String = If(byteCount >= 0, "", "-")
            byteCount = Math.Abs(byteCount)
            If byteCount < 1024 Then
                Return sym & byteCount & " Bytes"
            ElseIf byteCount < 1024 ^ 2 Then
                Return sym & (byteCount / 1024).ToString("F2") & " KiB"
            ElseIf byteCount < 1024 ^ 3 Then
                Return sym & (byteCount / 1024 ^ 2).ToString("F2") & " MiB"
            ElseIf Not More OrElse byteCount < 1024 ^ 4 Then
                Return sym & (byteCount / 1024 ^ 3).ToString("F2") & " GiB"
            ElseIf byteCount < 1024 ^ 5 Then
                Return sym & (byteCount / 1024 ^ 4).ToString("F2") & " TiB"
            Else
                Return sym & (byteCount / 1024 ^ 5).ToString("F2") & " PiB"
            End If
        End Function
    End Class

    Public Shared Function ReduceDataUnit(KBytes As Int64) As String
        Dim Result As Decimal = KBytes * 1000
        Dim ResultUnit As Integer = -6
        While Result >= 1000
            Result /= 1024
            ResultUnit += 3
        End While
        Dim ResultString As String = CStr(Math.Round(Result, 2))
        Select Case ResultUnit
            Case 0
                Return ResultString & " MiB"
            Case 3
                Return ResultString & " GiB"
            Case 6
                Return ResultString & " TiB"
            Case 9
                Return ResultString & " PiB"
            Case 12
                Return ResultString & " EiB"
            Case 15
                Return ResultString & " ZiB"
            Case 18
                Return ResultString & " YiB"
            Case Else
                Return ResultString & " << " & ResultUnit & "MiB"
        End Select
    End Function

    Public Shared Function RichByte2Hex(bytes As Byte(), Optional ByVal TextShow As Boolean = False) As String
        Const HalfWidthChars As String = "`~!@#$%^&*()_+-=|\ <>?,./:;""''{}[]0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
        If bytes Is Nothing Then Return ""
        If bytes.Length = 0 Then Return ""
        Dim sb As New StringBuilder
        Dim tb As String = ""
        Dim ln As New StringBuilder
        For i As Integer = 0 To bytes.Length - 1
            If i Mod 16 = 0 And TextShow Then
                ln.Append("|" & Hex(i).PadLeft(5) & "h: ")
            End If
            ln.Append(Convert.ToString((bytes(i) And &HFF) + &H100, 16).Substring(1).ToUpper)
            ln.Append(" ")
            Dim c As Char = Chr(bytes(i))
            If Not HalfWidthChars.Contains(c) Then
                tb &= "."
            Else
                tb &= c
            End If
            If i Mod 16 = 15 Then
                If TextShow Then
                    ln.Append(tb)
                End If
                sb.Append(ln.ToString().PadRight(74) & "|")
                sb.Append(vbCrLf)
                ln = New StringBuilder()
                tb = ""
            End If
        Next
        If TextShow And tb <> "" Then
            ln.Append(tb)
        End If
        If ln.Length > 0 Then sb.Append(ln.ToString().PadRight(74) & "|")
        Return sb.ToString()
    End Function
End Class