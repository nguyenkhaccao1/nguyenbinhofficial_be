using NguyenBinh.Shared.Authorization;

namespace NguyenBinh.UnitTests.Shared;

public class PermissionMatrixTests
{
    [Fact]
    public void Permission_codes_are_unique_and_well_formed()
    {
        var codes = Permissions.AllCodes.ToList();
        codes.Should().OnlyHaveUniqueItems();
        codes.Should().AllSatisfy(c => c.Should().MatchRegex("^[a-z]+\\.[a-z_]+$"));
    }

    [Fact]
    public void Every_default_role_permission_exists()
    {
        foreach (var role in SystemRoles.All)
            SystemRoles.DefaultPermissions(role).Should().AllSatisfy(p => Permissions.Exists(p).Should().BeTrue());
    }

    [Fact]
    public void SuperAdmin_has_everything_and_Admin_cannot_purge()
    {
        SystemRoles.DefaultPermissions(SystemRoles.SuperAdmin).Should().BeEquivalentTo(Permissions.AllCodes);
        SystemRoles.DefaultPermissions(SystemRoles.Admin).Should().NotContain(Permissions.System.Purge)
            .And.Contain(Permissions.Users.Create);
    }

    [Fact]
    public void Editor_manages_content_but_not_custom_html_leads_or_users()
    {
        var editor = SystemRoles.DefaultPermissions(SystemRoles.Editor);
        editor.Should().Contain([Permissions.Projects.Publish, Permissions.Blog.Create, Permissions.Media.Upload]);
        editor.Should().NotContain([Permissions.Pages.CustomHtml, Permissions.Leads.View, Permissions.Users.View,
            Permissions.Settings.Update]);
    }

    [Fact]
    public void Seo_role_can_edit_but_not_publish_or_delete_content()
    {
        var seo = SystemRoles.DefaultPermissions(SystemRoles.Seo);
        seo.Should().Contain([Permissions.Seo.Redirect, Permissions.Projects.Update, Permissions.Blog.Publish]);
        seo.Should().NotContain([Permissions.Projects.Publish, Permissions.Projects.Delete, Permissions.Leads.View]);
    }

    [Fact]
    public void Sales_handles_leads_without_delete()
    {
        var sales = SystemRoles.DefaultPermissions(SystemRoles.Sales);
        sales.Should().Contain([Permissions.Leads.View, Permissions.Leads.Assign, Permissions.Leads.Export]);
        sales.Should().NotContain([Permissions.Leads.Delete, Permissions.Projects.Update]);
    }

    [Fact]
    public void Viewer_is_read_only_and_cannot_see_leads()
    {
        var viewer = SystemRoles.DefaultPermissions(SystemRoles.Viewer);
        viewer.Should().AllSatisfy(p => p.Should().EndWith(".view"));
        viewer.Should().NotContain([Permissions.Leads.View, Permissions.Users.View, Permissions.Audit.View]);
    }
}
