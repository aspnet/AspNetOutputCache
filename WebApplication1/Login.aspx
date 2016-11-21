<%@ Page LANGUAGE="vb" %>

<script runat="server" >

   Private Sub Page_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        Me.Label1.Text = "NOT AUTHENTICATED"
        If Page.User.Identity.IsAuthenticated Then
            Me.Label1.Text = "YOU ARE IN"
        End If
    End Sub</script>

<form Runat="server" >
    <asp:ScriptManager ID="sm1" runat="server">
  <Scripts>
    <asp:ScriptReference Name="jquery"/>
  </Scripts>
</asp:ScriptManager>
	<asp:Login runat="server" ID="Login1" DestinationPageUrl="destination.aspx" ></asp:Login>
	<asp:Label runat="server" ID="Label1" ></asp:Label>
</form>