#Disable Warning IDE1006 ' 命名スタイル

'こちら、clsNDC.vbを適宜
Imports Microsoft.Data.SqlClient
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Reflection.MethodBase
Imports System.Security.Cryptography


Public Class clsNDC

#Region "■宣言・定数"
    '*******************************************************************************************
    '宣言・定数
    '*******************************************************************************************

    Public Const archiveRoot As String = "\\naranja.local\dfs02\archives"
    Public Const dfsRoot As String = "\\naranja.local\dfs02"

    Public Const customerId37Out As Integer = 4
    Public Const customerId38Out As Integer = 3
    Public Const customerId37In As Integer = 6
    Public Const customerId38In As Integer = 5
    Public Const customerId38ZensenModoshi As Integer = 7
    'これらのデータを作る時は、色々方法があるが、
    '(1)tbl000tbl101等の臨時のテーブルにコピーし、名簿IDのIDENTITYを削除し、
    '(2)必要なデータのIDを入れ替えて、SET IDENTITY_INSERT Contents ON -> 結果をINSERT ->OFF(以下参照)
    'SET IDENTITY_INSERT dbo.tbl101名簿 ON;
    'INSERT INTO dbo.tbl101名簿 (---ここにすべてのコラム---) SELECT ---ここにすべてのコラム--- FROM dbo.tbl000tbl101_20220202 WHERE (名簿ID = 12);
    'SET IDENTITY_INSERT dbo.tbl101名簿 OFF;
    Public Const customerIdKishoZaikoCheck As Integer = 12

    Public Const customerIdRaiten As Integer = 1451
    Public Const customerIdEventRaiten As Integer = 1437
    Public Const customerIdZaiko As Integer = 1453
    Public Const customerIdNyuka As Integer = 1450
    Public Const customerIdTwistersRaiten As Integer = 158989
    Public Const customerIdCenterFilmYobi As Integer = 161543 'ds200.tbl203在庫変遷.TableAdapter に含まれています

    Public Const customerIdAmazon As Integer = 158656 'Amazon受注用

    Public Const dtmNothing As DateTime = #12/31/2049#
    Public Const dtmEmpty As DateTime = #12/30/2049# '日付の「空白」を明示するための日付
    Public Const dtmNull As DateTime = #1/1/1901# 'clsndc.nzdatetime(***)のデフォルト値、NULL利用
    Public Const dtmUrgent As DateTime = #1/1/2001#
    Public Const orderIdAllChecked As Integer = 1000000 '伝票検索などでの範囲絞込用:過去の未入金などで残したいものはオーダーを立てる。
    Public Const outputDocumentIdAllChecked As Integer = 1800000 '伝票検索などでの範囲絞込用(出力伝票ID)

    Public Const hachuIdAllChecked As Integer = 15000 '発注関係の範囲絞込用
    Public Const dcmTax As Decimal = CDec(10.0) ' 2019/10/1改定
    Public Const bigInteger As Integer = 999999999 '何かの比較用 本来は 2147483647 だが、この数にしてしまうと、足し算等で簡単にオーバーフローになる。
    Public Const bigLong As Long = 99999999999999999 '何かの比較用 本来は  9223372036854775807 だが、この数にしてしまうと、足し算等で簡単にオーバーフローになる。

    Public Const impossibleString As String = "蜿叡" '検索時に絶対引っかからない文字

    Public Const shtWorkingStaffIdAccountManager As Short = 63 '経理 古堺さん
    Public Const laborManager As Short = 1 '労務

#End Region '■宣言・定数


#Region "■汎用関数"
    '*******************************************************************************************
    '汎用関数
    '*******************************************************************************************
#Region "判定グローバル関数"
    Public Shared Function IsInteger(o As Object) As Boolean
        Return o IsNot Nothing AndAlso o.ToString.EndsWith(clsNDC.NzInteger(o).ToString)
    End Function

    Public Shared Function IsFormArive(o As Object) As Boolean
        If o Is Nothing Then
            Return False
        End If
        If TypeOf o Is Form Then
            Return CType(o, Form).IsHandleCreated
        End If
        Return False

    End Function


#End Region

#Region "エラーメッセージ"
    Public Shared Function EM(ByVal strMessage As String, Optional ByVal strTitle As String = "エラー", Optional ByVal ask As Boolean = True) As Boolean
        If ask Then
            MessageBox.Show(strMessage, strTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If

        Return False
    End Function

    Public Shared Function EM(ByVal ex As Exception, Optional ByVal strTitle As String = "エラー", Optional ByVal ask As Boolean = True) As Boolean
        Return clsNDC.EM(String.Format("{1}{0}{0}{2}", vbCrLf, ex.Message, ex.StackTrace), strTitle, ask)
    End Function

#End Region

#Region "文字列操作"
    Public Shared Function IsNarrow(ByVal s As String) As Boolean

        Dim i As Integer
        Dim intCheckChar As Integer
        Dim blnRet As Boolean

        blnRet = True
        For i = 1 To Len(s)
            intCheckChar = Asc(Mid(s, i, 1))
            If intCheckChar < 0 Or intCheckChar > 255 Then
                blnRet = False
                Exit For
            End If
        Next
        Return blnRet

    End Function

    Public Shared Function ToNarrow(sourceString As String) As String
        '全角英数字記号空白を半角に。ただし、＆＜＞は除く(Web上でのエラーを防ぐため)
        If String.IsNullOrEmpty(sourceString) Then
            Return String.Empty
        End If
        Cursor.Current = Cursors.WaitCursor

        Return New Regex("[０-９Ａ-Ｚａ-ｚ＃＄％’（）＊＋，－．／：；＝？＠［］￥＾＿｛｜｝　]").Replace(sourceString, AddressOf clsNDC.myReplacer)
    End Function

    Public Shared Function Utf16Decode(sourceString As String) As String
        '\u98a8 形式のUTF16データをデコード
        If String.IsNullOrEmpty(sourceString) Then
            Return String.Empty
        End If
        Dim rx As New Regex("\\[uU]([0-9A-Fa-f]{4})")
        Return rx.Replace(sourceString, Function(match) CChar(ChrW(Int32.Parse(match.Value.Substring(2), Globalization.NumberStyles.HexNumber))).ToString())

    End Function

    Private Shared Function myReplacer(ByVal m As Match) As String
        Return Strings.StrConv(m.Value, VbStrConv.Narrow, 0)
    End Function

    Public Shared Function EscapeHTML(ByVal s As String) As String
        '文字列を、HTML エンコードされた文字列に変換。
        '※htmlSpecialCharsはclsweb.htmlSpecialChars！
        'ナランハにてHTML用にEscape = 全角記号英数字を半角に(&を除く)
        s = clsNDC.ToNarrow(s)
        Return System.Net.WebUtility.HtmlEncode(s)
    End Function

    Public Shared Function EscapeJson(ByVal s As String) As String
        s = Regex.Replace(s, "[""\\/]", "\$&")
        s = Replace(s, vbCrLf, "\n\r")
        Return s
    End Function

    Public Shared Function EscapeSQLLike(ByVal s As String, Optional escapeSingleQuote As Boolean = False) As String
        If String.IsNullOrEmpty(s) Then
            Return String.Empty
        Else
            'escapeSingleQuote --- Like N'(ここに置く)' 場合にSingleQuoteは重ねる
            If escapeSingleQuote Then
                s = Replace(s, "'", "''")
            End If
            ']はエスケープの必要なし
            Return Regex.Replace(s, "([%_^\[])", "[$1]")
        End If
    End Function

    Public Shared Function EscapeSQLLikeWithWild(ByVal s As String, Optional escapeSingleQuote As Boolean = False) As String
        If String.IsNullOrEmpty(s) Then
            Return "%"
        Else
            'escapeSingleQuote --- Like N'(ここに置く)' 場合にSingleQuoteは重ねる
            If escapeSingleQuote Then
                s = Replace(s, "'", "''")
            End If
            ']はエスケープの必要なし
            Return "%" & Regex.Replace(s, "([%_^\[])", "[$1]") & "%"
        End If
    End Function

    Public Shared Function EscapeVbNetRegEx(ByVal s As String) As String
        If String.IsNullOrEmpty(s) Then
            Return String.Empty
        Else
            Return Regex.Replace(s, "(?<1>[.\\])", "\$1")
        End If
    End Function

    Public Shared Function EscapeSQLString(ByVal s As String, Optional allowNull As Boolean = True) As String
        If String.IsNullOrEmpty(s) Then
            If allowNull Then
                Return "NULL"
            Else
                Return "N''"
            End If
        Else
            '改行コードを統一
            s = Replace(s, vbCrLf, vbLf)
            s = Replace(s, vbCr, vbLf)
            s = Replace(s, vbLf, vbCrLf)

            Return Replace("N'" & Replace(s, "'", "''") & "'", vbCrLf, "' + NCHAR(13) + NCHAR(10) + N'")
        End If
    End Function

    Public Shared Function DisplayName(ByVal strSource As String) As String
        If String.IsNullOrEmpty(strSource) Then
            Return String.Empty
        End If
        If Regex.IsMatch(strSource, "【.+?】") Then
            strSource = Regex.Replace(strSource, "【.+?】", String.Empty)
        End If
        If String.IsNullOrEmpty(strSource) Then
            Return String.Empty
        End If
        strSource = Replace(strSource, " ---", "")
        If String.IsNullOrEmpty(strSource) Then
            Return String.Empty
        End If
        Return Trim(Regex.Replace(strSource, " +", " "))
    End Function

    Public Shared Function 敬称(ByVal strCompany As String, ByVal strDepartment As String, ByVal strSei As String, ByVal strMei As String, ByVal ifCrLf As Boolean) As String
        Dim strAtena As String = String.Empty

        If Not String.IsNullOrEmpty(strDepartment) Then strAtena &= strDepartment & " "
        If Not String.IsNullOrEmpty(strSei) Then strAtena &= strSei & " "
        If Not String.IsNullOrEmpty(strMei) Then strAtena &= strMei

        If String.IsNullOrEmpty(Trim(strCompany)) Then
            strAtena &= " 様"
        Else
            If String.IsNullOrEmpty(Trim(strAtena)) Then
                strAtena = Trim(strCompany) & " 御中"
            Else
                Dim strCrLf As String = " "
                If ifCrLf Then strCrLf = vbCrLf
                strAtena = Trim(strCompany) & strCrLf & strAtena & " 様"
            End If
        End If
        Return strAtena

    End Function

    Public Shared Function 検索用総合名設定(ByVal ctl0 As Control, ByVal ctl1 As Control, ByVal ctl2 As Control, ByVal ctl3 As Control, ByVal ctl4 As Control, ByVal ctl5 As Control, ByVal ctl6 As Control, ByVal ctl7 As Control) As String
        '姓 名 フリガナ姓 フリガナ名 法人団体名 部署役職名, EMail の順番 ※最後は楽天の判定用
        Try
            Dim strTokushu As String
            If ctl6 IsNot Nothing Then
                strTokushu = Trim(ctl6.Text)
            Else
                strTokushu = String.Empty
            End If

            Dim strRakuten As String
            If ctl7 IsNot Nothing AndAlso Trim(ctl7.Text).EndsWith(".fw.rakuten.ne.jp") Then
                strRakuten = "(楽天)"
            Else
                strRakuten = String.Empty
            End If

            Dim strSougoumei As String = Trim(String.Format("{7}{6}{4} {5} {0}{1} {2}{3}", Trim(ctl0.Text), Trim(ctl1.Text), Trim(ctl2.Text), Trim(ctl3.Text), Trim(ctl4.Text), Trim(ctl5.Text), strTokushu, strRakuten))
            strSougoumei = Trim(Regex.Replace(strSougoumei, "[ ]+", " "))
            If strSougoumei.Length > 0 Then strSougoumei = " " & strSougoumei & " "
            Return strSougoumei
        Catch ex As Exception
            MessageBox.Show(ex.Message & vbCrLf & vbCrLf & ex.StackTrace, GetCurrentMethod.Name)
            Return String.Empty
        End Try
    End Function


#End Region

#Region "NDC型変換(Nz...)"
    Public Shared Function NzBoolean(ByVal o1 As Object, Optional ByVal DefaultValue As Boolean = False) As Boolean
        If IsNothing(o1) Then
            Return DefaultValue
        End If
        If o1.ToString.Equals("true", StringComparison.OrdinalIgnoreCase) Then
            Return True
        ElseIf o1.ToString.Equals("false", StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If
        Try
            Return CType(o1, Boolean)
        Catch ex As Exception
            Return DefaultValue
        End Try
    End Function

    Public Shared Function NzYen(ByVal o1 As Object, Optional ByVal DefaultValue As Integer = 0) As Integer
        If IsNothing(o1) Then Return DefaultValue
        Dim s As String = o1.ToString
        Dim intYen As Integer

        If Integer.TryParse(s, Globalization.NumberStyles.Any, Nothing, intYen) Then
            Return intYen
        Else
            s = Regex.Replace(s, "[\\￥,円]", String.Empty)
            s = Regex.Replace(s, "[\s]*JPY", String.Empty)

            If IsNumeric(s) Then
                Return CType(s, Integer)
            Else
                Return DefaultValue
            End If
        End If

    End Function

    Public Shared Function NzInteger(ByVal o1 As Object, Optional ByVal DefaultValue As Integer = 0) As Integer
        'o1がboolean の場合 そのままだとtrue:-1 false:0になります。注意。
        If TypeOf o1 Is Boolean Then
            If CType(o1, Boolean) Then Return 1 Else Return 0
        ElseIf IsNumeric(o1) AndAlso Math.Abs(CType(o1, Double)) < CType(&H7FFFFFFF, Double) Then
            Return CType(o1, Integer)
        Else
            If o1 IsNot Nothing AndAlso o1.ToString.Contains("."c) Then
                Return clsNDC.NzInteger(o1.ToString.Substring(0, o1.ToString.IndexOf("."c)), DefaultValue)
            End If
            Return DefaultValue
        End If

    End Function

    Public Shared Function NzLong(ByVal o1 As Object, Optional ByVal DefaultValue As Long = 0) As Long
        If TypeOf o1 Is Boolean Then
            If CType(o1, Boolean) Then Return 1 Else Return 0
        ElseIf IsNumeric(o1) AndAlso Math.Abs(CType(o1, Double)) < CType(&H7FFFFFFFFFFFFFFF, Double) Then
            Return CType(o1, Long)
        Else
            If o1 IsNot Nothing AndAlso o1.ToString.Contains("."c) Then
                Return clsNDC.NzLong(o1.ToString.Substring(0, o1.ToString.IndexOf("."c)), DefaultValue)
            End If
            Return DefaultValue
        End If
    End Function

    Public Shared Function NzShort(ByVal o1 As Object, Optional ByVal DefaultValue As Short = 0) As Short
        Dim i As Integer = clsNDC.NzInteger(o1, CInt(DefaultValue))
        If i >= -32768 AndAlso i <= 32767 Then Return CType(i, Short) Else Return DefaultValue
    End Function

    Public Shared Function NzByte(ByVal o1 As Object, Optional ByVal DefaultValue As Byte = 0) As Byte
        Dim i As Integer = clsNDC.NzInteger(o1, CInt(DefaultValue))
        If i >= 0 AndAlso i <= 255 Then Return CType(i, Byte) Else Return DefaultValue
    End Function

    Public Shared Function NzDecimal(ByVal o1 As Object, Optional ByVal DefaultValue As Decimal = 0D, Optional digit As Integer = -1) As Decimal
        If IsNumeric(o1) Then
            If digit < 0 Then
                Return CType(o1, Decimal)
            Else
                '桁数分かけて0.5を足して四捨五入して割る
                Return CType(Math.Floor(CType(o1, Decimal) * 10 ^ digit + 0.5D) / 10 ^ digit, Decimal)
            End If
        Else
            Return DefaultValue
        End If

    End Function

    Public Shared Function NzString(ByVal o1 As Object, Optional ByVal DefaultValue As String = "") As String
        Dim strTemp As String
        If Not IsNothing(o1) Then
            strTemp = o1.ToString
            If String.IsNullOrEmpty(strTemp) Then strTemp = DefaultValue
            Return strTemp
        Else
            Return DefaultValue
        End If
    End Function

    Public Shared Function NzDateTime(ByVal o1 As Object, Optional ByVal DefaultValue As DateTime = #1/1/1901#) As DateTime
        Dim dtm As DateTime = DefaultValue

        If IsNothing(o1) Then
            Return dtm
        ElseIf DateTime.TryParse(o1.ToString, dtm) Then
            Return dtm
        ElseIf DateTime.TryParseExact(o1.ToString, "yyyyMMdd", Nothing, Globalization.DateTimeStyles.None, dtm) Then
            Return dtm
        ElseIf DateTime.TryParseExact(o1.ToString, "yyyy-MM-dd", Nothing, Globalization.DateTimeStyles.None, dtm) Then
            Return dtm
        ElseIf DateTime.TryParseExact(o1.ToString, "yyyy.M.d", Nothing, Globalization.DateTimeStyles.None, dtm) Then
            Return dtm
        Else
            Return DefaultValue
        End If

    End Function
#End Region

    Private Shared Function checkSumOfFolders(directoryFullPath As String) As Integer
        Dim sbForChecksum As New StringBuilder
        For Each eachFile As String In Directory.GetFiles(directoryFullPath, "*", SearchOption.AllDirectories).OrderBy(Function(n) n)
            If Path.GetExtension(eachFile) <> ".db" AndAlso Path.GetExtension(eachFile) <> ".Identifier" Then
                sbForChecksum.Append(checkSumOfFiles(eachFile))
            End If
        Next
        Return sbForChecksum.ToString.GetHashCode

    End Function

    Private Shared Function checkSumOfFiles(fileFullPath As String) As String

        If Not File.Exists(fileFullPath) Then
            Return String.Empty
        End If

        Using fs As New FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            Dim bs As Byte() = MD5.Create().ComputeHash(fs)
            Dim result As New System.Text.StringBuilder()
            For Each b As Byte In bs
                result.Append(b.ToString("x2"))
            Next
            Return result.ToString
        End Using

        Return String.Empty

    End Function


    Public Shared Function MirrorFolder(ask As Boolean, deadline As DateTime, src As String, dest As String) As Boolean
        Try

            If DateTime.Now > deadline Then
                Return False
            End If

            If Not Directory.Exists(src) Then
                Return clsNDC.EM(String.Format("存在するはずのフォルダ{0}{0}{1}{0}{0}が存在しません。", vbCrLf, src))
            End If

            If Not Directory.Exists(dest) Then
                Directory.CreateDirectory(dest)
            End If

            Cursor.Current = Cursors.WaitCursor

            If clsNDC.checkSumOfFolders(src) = clsNDC.checkSumOfFolders(dest) Then
                'コピーしない、ここデバッグのため残す
            Else
                If ask AndAlso MessageBox.Show("NDCによるフォルダの更新が指示されました。このままOKをクリックしてください。", "キャンセルを一応できるように", MessageBoxButtons.OKCancel) <> DialogResult.OK Then
                    Return False
                End If
                My.Computer.FileSystem.DeleteDirectory(dest, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.DeletePermanently, FileIO.UICancelOption.DoNothing)
                My.Computer.FileSystem.CopyDirectory(src, dest, True)
            End If

            Return True
        Catch ex As Exception
            Return clsNDC.EM(String.Format("フォルダのコピー{0}{0}{1}{0}↓{2}{0}{0}に失敗しました。{0}{0}{3}", vbCrLf, src, dest, ex.Message))
        End Try

    End Function

#End Region '■汎用関数


#Region "■データベース/各種値取得/更新・同時実行関連"
    '*******************************************************************************************
    'データベース/各種値取得/更新・同時実行関連
    '*******************************************************************************************
#Region "データベース(SQLServer)"

    'HereString ヒアストリングを作成
    'clsNDC.HereString(<Q><![CDATA[
    ' --- ここに文章 {0} {1} {2d} {3G}等 変数展開可 ただし ]]> 不可
    ' ※最初と最後の改行のみ削除します。
    ']]></Q>.Value[, 以下変数展開用])

    '※[ツール]-[オプション]-[環境]-[フォントおよび色]-[表示項目:VBのXML CDataセクション:前景色]を変えると見やすくなる
    Public Shared Function HereString(ByVal source As String, ParamArray params As Object()) As String
        source = clsNDC.convertParams(source, params)

        'DOMオブジェクトは改行がLFになる
        source = Replace(source, vbLf, vbCrLf)

        If source.StartsWith(vbCrLf) Then
            source = source.Substring(2)
        End If
        If source.EndsWith(vbCrLf) Then
            source = source.Substring(0, source.Length - vbCrLf.Length)
        End If

        Return source

    End Function

    'convertParams String.Formatの拡張 (日付型を適切に変換)
    '
    '@param String source 元となる文字列。 
    '@param Object() params source内の{0},{1}... を変換する内容
    '@return String 変換後のsource

    Private Shared Function convertParams(source As String, ParamArray params As Object()) As String

        '{***}で数字で始まらないものを置き換え 他で使わない文字を一時的に利用 { ⇒ 丅 } ⇒ 亠 ※IME 2画の漢字より選出
        source = Regex.Replace(source, "{(?<1>[0-9][^}]*)}", "丅$1亠")
        source = Regex.Replace(source, "{", "{{")
        source = Regex.Replace(source, "}", "}}")
        source = Regex.Replace(source, "丅(?<1>[^亠]+)亠", "{$1}")

        '日付型を変換
        '{*d} => d型(日付)
        '{*G} => G型(日時)
        source = Regex.Replace(source, "{(?<1>[0-9]+)d}", "CONVERT(DATETIME, '{$1:yyyy-MM-dd} 00:00:00', 102)")
        source = Regex.Replace(source, "{(?<1>[0-9]+)G}", "CONVERT(DATETIME, '{$1:yyyy-MM-dd HH:mm:ss}', 102)")

        Return String.Format(source, params)

    End Function

    'SetSqlParams 以下SQLのパラメータをhashtableに格納する関数
    Public Shared Function SetSqlParams(ByRef ht As Hashtable, key As String, dbType As SqlDbType, limit As Integer, value As Object) As Boolean
        Try
            Dim arlValue As New ArrayList From {dbType, limit, value}
            ht(key) = arlValue
            Return True
        Catch ex As Exception
            Return clsNDC.EM(ex, GetCurrentMethod.Name)
        End Try
    End Function

    'SqlHere SQLをパラメータ形式にて記載
    'clsNDC.SqlHere(" --- SQL パラメータを@*** 形式で(直接挿入できる形 ", ht)
    'もしくは(ヒアドキュメント)
    'clsNDC.SqlHere(<Q><![CDATA[
    ' --- SQL パラメータを@*** 形式で(直接挿入できる形)
    ']]></Q>.Value, ht)
    '
    'ht --- パラメータの値を入れるhashtable
    '       key:パラメータ名
    '       value:パラメータ型(SQLServer) パラメータサイズ パラメータ値 の順で入ったarraylist

    'defaultvalue --- 無い場合はexecuteNonQuery 他はexecutescalar
    'insert --- true時：挿入されたIDを返す

    '@return defaultvalue に基づく スカラー値 指定しない場合はExecuteNonQueryの値を返す
    Public Shared Function SqlHere(ByVal command As String, ht As Hashtable, Optional defaultValue As Object = Nothing, Optional insert As Boolean = False) As Object
        Try
            Using connectionNDC As New SqlConnection(My.Settings.db_naranjaConnectionString)
                If insert Then
                    command &= "; SELECT SCOPE_IDENTITY();"
                End If
                Dim commandNDC As New SqlCommand(command, connectionNDC)
                For Each de As DictionaryEntry In ht
                    If TypeOf de.Value Is ArrayList Then
                        Dim paramData As ArrayList = CType(de.Value, ArrayList)
                        If clsNDC.NzInteger(paramData(1)) > 0 Then
                            commandNDC.Parameters.Add(de.Key.ToString, CType(paramData(0), SqlDbType), clsNDC.NzInteger(paramData(1)))
                        Else
                            commandNDC.Parameters.Add(de.Key.ToString, CType(paramData(0), SqlDbType))
                        End If
                        commandNDC.Parameters(de.Key.ToString).Value = paramData(2)
                    End If
                Next
                connectionNDC.Open()
                commandNDC.CommandTimeout = 600
                If defaultValue Is Nothing Then
                    Return commandNDC.ExecuteNonQuery()
                Else
                    Dim result As Object = commandNDC.ExecuteScalar()
                    If result Is Nothing OrElse result.Equals(DBNull.Value) Then
                        Return defaultValue
                    Else
                        Return result
                    End If
                End If
            End Using
        Catch ex As Exception
            Clipboard.SetText(command)
            MessageBox.Show(command & vbCrLf & vbCrLf & ex.Message & vbCrLf & vbCrLf & ex.StackTrace, GetCurrentMethod.Name)
            Return DBNull.Value
        End Try

    End Function

    Public Shared Function SqlHereNonQuery(ByVal command As String, ht As Hashtable) As Integer
        'SqlHereは汎用的にobjectを返す、こちら、ExecuteNonQueryNDCと同様にintegerを返す！
        Return clsNDC.NzInteger(clsNDC.SqlHere(command, ht))
    End Function

    Public Shared Function SqlHereInsert(ByVal command As String, ht As Hashtable) As Integer
        'SqlHereInsertはSELECT挿入時に正しくIDENTITY列を返さない！
        If Not String.IsNullOrEmpty(command) AndAlso Regex.IsMatch(command, "\).+SELECT", RegexOptions.Singleline) Then
            MessageBox.Show("SqlHereInsertはSELECT挿入時に正しくIDENTITY列を返しません。やり直してください。", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return -1
        Else
            Return clsNDC.NzInteger(clsNDC.SqlHere(command, ht, 0, True))
        End If
    End Function

    Public Shared Function SqlHereInteger(ByVal command As String, ht As Hashtable, Optional defaultValue As Integer = 0) As Integer
        Return clsNDC.NzInteger(clsNDC.SqlHere(command, ht, defaultValue), defaultValue)
    End Function

    Public Shared Function SqlHereShort(ByVal command As String, ht As Hashtable, Optional defaultValue As Short = 0) As Short
        Return clsNDC.NzShort(clsNDC.SqlHere(command, ht, defaultValue), defaultValue)
    End Function

    Public Shared Function SqlHereString(ByVal command As String, ht As Hashtable, Optional defaultValue As String = "") As String
        Return clsNDC.NzString(clsNDC.SqlHere(command, ht, defaultValue), defaultValue)
    End Function

    Public Shared Function GetSqlDateTime(defaultValue As DateTime, ByVal command As String, ParamArray params As Object()) As DateTime
        Return clsNDC.NzDateTime(clsNDC.ExecuteScalarNDC(0, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlDateTime(dbNumber As Integer, defaultValue As DateTime, ByVal command As String, ParamArray params As Object()) As DateTime
        Return clsNDC.NzDateTime(clsNDC.ExecuteScalarNDC(dbNumber, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlBoolean(defaultValue As Boolean, ByVal command As String, ParamArray params As Object()) As Boolean
        Return clsNDC.NzBoolean(clsNDC.ExecuteScalarNDC(0, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlShort(defaultValue As Short, ByVal command As String, ParamArray params As Object()) As Short
        Return clsNDC.NzShort(clsNDC.ExecuteScalarNDC(0, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlInteger(defaultValue As Integer, ByVal command As String, ParamArray params As Object()) As Integer
        Return clsNDC.NzInteger(clsNDC.ExecuteScalarNDC(0, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlInteger(dbNumber As Integer, defaultValue As Integer, ByVal command As String, ParamArray params As Object()) As Integer
        Return clsNDC.NzInteger(clsNDC.ExecuteScalarNDC(dbNumber, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlLong(defaultValue As Long, ByVal command As String, ParamArray params As Object()) As Long
        Return clsNDC.NzLong(clsNDC.ExecuteScalarNDC(0, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlDecimal(defaultValue As Decimal, ByVal command As String, ParamArray params As Object()) As Decimal
        Return clsNDC.NzDecimal(clsNDC.ExecuteScalarNDC(0, command, params), defaultValue)
    End Function

    Public Shared Function GetSqlString(ByVal command As String, ParamArray params As Object()) As String
        Return clsNDC.NzString(clsNDC.ExecuteScalarNDC(0, command, params))
    End Function

    Public Shared Function GetSqlString(dbNumber As Integer, ByVal command As String, ParamArray params As Object()) As String
        Return clsNDC.NzString(clsNDC.ExecuteScalarNDC(dbNumber, command, params))
    End Function

    'ExecuteNonQueryNDC SQLコマンド Insert で 挿入したID列を返す
    Public Shared Function ExecuteInsert(ByVal command As String, ParamArray params As Object()) As Integer

        Return clsNDC.NzInteger(clsNDC.ExecuteScalarNDC(0, command & "; SELECT SCOPE_IDENTITY();", params))

    End Function

    'ExecuteNonQueryNDC SQLコマンド ExecuteNonQuery の直接実行(
    Public Shared Function ExecuteNonQueryNDC(ask As Boolean, dbNumber As Integer, ByVal command As String, ParamArray params As Object()) As Integer
        Try
            'dbNumber --- 1000 * 試行回数 + データベース番号 現状2回まで
            If dbNumber > 2000 Then
                clsNDC.EM(String.Format("データベース トランザクションエラー - 試行回数が超過しました。{0}{0}{1}", vbCrLf, command), "エラー", ask)
                Return -1
            End If

            command = clsNDC.convertParams(command, params)

            Dim dbConnectionString As String

            Select Case dbNumber
                Case 0
                    dbConnectionString = My.Settings.db_naranjaConnectionString
                Case 1
                    dbConnectionString = My.Settings.db_naranja_archiveConnectionString
                Case Else
                    Return -1
            End Select
            Using connectionNDC As New SqlConnection(dbConnectionString)
                Dim commandNDC As New SqlCommand(command, connectionNDC)
                connectionNDC.Open()
                commandNDC.CommandTimeout = 600
                Return commandNDC.ExecuteNonQuery()
            End Using
        Catch ex As Exception
            If ex.Message.StartsWith("トランザクション") Then
                'トランザクション失敗 - 2秒待って再試行
                Threading.Thread.Sleep(2000)
                Return clsNDC.ExecuteNonQueryNDC(ask, dbNumber + 1000, command, params)
            End If
            clsNDC.EM(String.Format("{1}{0}{0}{2}{0}{0}{3}", vbCrLf, command, ex.Message, ex.StackTrace), GetCurrentMethod.Name, ask)
            Return -1
        End Try
    End Function

    'ExecuteNonQueryNDC SQLコマンド ExecuteNonQuery の直接実行(現DB)
    Public Shared Function ExecuteNonQueryNDC(ByVal command As String, ParamArray params As Object()) As Integer
        Return clsNDC.ExecuteNonQueryNDC(True, 0, command, params)
    End Function

    'ExecuteNonQueryNDC SQLコマンド ExecuteNonQuery の直接実行(DB選択バージョン)
    Public Shared Function ExecuteNonQueryNDC(dbNumber As Integer, ByVal command As String, ParamArray params As Object()) As Integer
        Return clsNDC.ExecuteNonQueryNDC(True, dbNumber, command, params)
    End Function

    'ExecuteScalarNDC SQLコマンド ExecuteScalar のラッパー
    'DBを選択できる
    Public Shared Function ExecuteScalarNDC(ask As Boolean, dbNumber As Integer, ByVal commandString As String, ParamArray params As Object()) As Object
        Try

            'dbNumber --- 1000 * 試行回数 + データベース番号 0:db_naranja 1:db_naranja_archive
            If dbNumber > 2000 Then
                clsNDC.EM(String.Format("データベース トランザクションエラー - 試行回数が超過しました。{0}{0}{1}", vbCrLf, commandString), "エラー", ask)
                Return DBNull.Value
            End If

            commandString = clsNDC.convertParams(commandString, params)

            Dim dbConnectionString As String

            Select Case dbNumber
                Case 0
                    dbConnectionString = My.Settings.db_naranjaConnectionString
                Case 1
                    dbConnectionString = My.Settings.db_naranja_archiveConnectionString
                Case Else
                    Return DBNull.Value
            End Select


            Using connectionNDC As New SqlConnection(dbConnectionString)
                Dim commandNDC As New SqlCommand(commandString, connectionNDC)
                connectionNDC.Open()
                commandNDC.CommandTimeout = 600
                Return commandNDC.ExecuteScalar()
            End Using
        Catch ex As Exception
            If ex.Message.StartsWith("トランザクション") Then
                'トランザクション失敗 - 2秒待って再試行
                Threading.Thread.Sleep(2000)
                Return clsNDC.ExecuteNonQueryNDC(dbNumber + 1000, commandString, params)
            End If
            clsNDC.EM(String.Format("{1}{0}{0}{2}{0}{0}{3}", vbCrLf, Command, ex.Message, ex.StackTrace), GetCurrentMethod.Name, ask)
            Return DBNull.Value
        End Try
    End Function

    Public Shared Function ExecuteScalarNDC(dbNumber As Integer, ByVal command As String, ParamArray params As Object()) As Object
        Return clsNDC.ExecuteScalarNDC(True, dbNumber, command, params)
    End Function

    Public Shared Function ExecuteScalar長文字列対策(tableName As String, idColumnName As String, id As Integer, settingColumnName As String, settingString As String) As Boolean
        Try
            Dim settingStrings As New List(Of String)
            Dim splitLength As Integer = 2000
            Do While settingString.Length > 0
                Dim cuttingLength = splitLength
                If settingString.Length < cuttingLength Then
                    cuttingLength = settingString.Length
                End If
                settingStrings.Add(settingString.Substring(0, cuttingLength))
                settingString = settingString.Substring(cuttingLength)
            Loop
            clsNDC.ExecuteNonQueryNDC("UPDATE {0} Set {3} = N'' WHERE ({1} = {2})", tableName, idColumnName, id, settingColumnName)
            For Each eachSettingString In settingStrings
                clsNDC.ExecuteNonQueryNDC("UPDATE {0} SET {3} = {3} + {4} WHERE ({1} = {2})", tableName, idColumnName, id, settingColumnName, clsNDC.EscapeSQLString(eachSettingString, False))
            Next
            Return True
        Catch ex As Exception
            Return clsNDC.EM(ex, GetCurrentMethod.Name)
        End Try

    End Function

#End Region

#Region "データベースより値取得"

    Public Shared Function リソース(ByVal shtID As Short, Optional ByVal intColumn As Integer = 1, Optional english As Boolean = False) As String
        Dim englishColumn As String
        If english Then
            englishColumn = "英語"
        Else
            englishColumn = String.Empty
        End If
        Select Case intColumn
            Case 1
                Return clsNDC.GetSqlString("SELECT リソース名1{1} FROM tbl011リソース WHERE (リソースID = {0})", shtID, englishColumn)
            Case 2
                Return clsNDC.GetSqlString("SELECT リソース名2{1} FROM tbl011リソース WHERE (リソースID = {0})", shtID, englishColumn)
            Case 3
                Return clsNDC.GetSqlString("SELECT リソース名3{1} FROM tbl011リソース WHERE (リソースID = {0})", shtID, englishColumn)
            Case 4
                Return clsNDC.GetSqlString("SELECT リソースメモ1{1} FROM tbl011リソース WHERE (リソースID = {0})", shtID, englishColumn)
            Case Else
                Return String.Empty
        End Select
    End Function

    Public Shared Function 情報リソース(ByVal intID As Integer, Optional ByVal intColumn As Integer = 1) As String
        Dim strResult As String
        Select Case intColumn
            Case 1
                strResult = clsNDC.GetSqlString("SELECT 情報リソース内容1 FROM tbl013情報リソース WHERE (情報リソースID = {0})", intID)
            Case 2
                strResult = clsNDC.GetSqlString("SELECT 情報リソース内容2 FROM tbl013情報リソース WHERE (情報リソースID = {0})", intID)
            Case 11
                strResult = clsNDC.GetSqlString("SELECT 情報リソース内容1 FROM tbl013情報リソース WHERE (情報リソースID = {0}) AND (情報リソースbit1 = 1)", intID)
            Case Else
                strResult = String.Empty
        End Select
        '結果を変更
        strResult = Replace(strResult, "[担当者]", clsNDC.スタッフ名(1))
        strResult = Replace(strResult, "[担当姓]", clsNDC.スタッフ名(4))
        strResult = Replace(strResult, "[本日]", DateTime.Today.ToString("yyyy/MM/dd"))

        If Not String.IsNullOrEmpty(strResult) AndAlso strResult.Contains("[メール署名:") Then
            strResult = Replace(strResult, "[メール署名:company]", clsNDC.情報リソース(328))
            strResult = Replace(strResult, "[メール署名:juggling]", clsNDC.情報リソース(330))
            strResult = Replace(strResult, "[メール署名:balloon]", clsNDC.情報リソース(332))
            strResult = Replace(strResult, "[メール署名:bar]", clsNDC.情報リソース(331))
        End If

        Return strResult
    End Function

    Public Shared Function 情報リソース代入(source As String) As String
        If String.IsNullOrEmpty(source) Then
            Return source
        End If
        Dim m As Match = Regex.Match(source, "情報リソース\(ID:(?<1>[0-9]+)\)")
        Dim resourceInfoIds As New List(Of Integer)
        Do While m.Success
            resourceInfoIds.Add(clsNDC.NzInteger(m.Groups(1).Value))
            m = m.NextMatch()
        Loop
        For Each resourceInfoId In resourceInfoIds
            source = Replace(source, String.Format("情報リソース(ID:{0})", resourceInfoId), clsNDC.情報リソース(resourceInfoId))
        Next
        Return source

    End Function

    Public Shared Function スタッフ名(ByVal intType As Integer, Optional ByVal staffId As Short = -999) As String
        '0:フルネーム / 1:短縮名 / 2:英語名
        Dim strFieldName As String
        Select Case intType
            Case 0 : strFieldName = "名前"
            Case 1 : strFieldName = "略称"
            Case 2 : strFieldName = "英語名"
            Case 3 : strFieldName = "英語略称"
            Case 4 : strFieldName = "姓"
                '↓KOT用
            Case 5 : strFieldName = "ISNULL(姓, N'') + N' ' + ISNULL(名, N'')"
            Case Else : strFieldName = "Error"
        End Select
        If staffId = -9 Then
            Return String.Empty
        End If
        If staffId = -999 Then
            staffId = -3
        End If
        Return GetSqlString("SELECT {0} FROM tbl021スタッフ WHERE スタッフID = {1}", strFieldName, staffId)
    End Function

    Private Shared Function PC名(ByVal intPCID As Integer) As String
        Return clsNDC.GetSqlString("SELECT PC名 FROM tbl701PC番号 WHERE PC番号 = {0}", intPCID)
    End Function

#End Region


#End Region '■データベース/各種値取得/更新・同時実行関連

End Class

