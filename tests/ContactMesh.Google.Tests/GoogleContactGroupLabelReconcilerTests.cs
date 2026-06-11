// File: GoogleContactGroupLabelReconcilerTests.cs
// Author: Zunair
// Producer: Copilot

using ContactMesh.Google.Contacts;
using Xunit;

namespace ContactMesh.Google.Tests
{
    public sealed class GoogleContactGroupLabelReconcilerTests
    {
        [Fact]
        public void CreatePlan_Creates_Missing_AppOwned_Labels()
        {
            var reconciler = new GoogleContactGroupLabelReconciler();

            var plan = reconciler.CreatePlan(
                "contact-mesh",
                new[] { "Directory", "directory", "Sales" },
                Array.Empty<GoogleContactGroupLabel>());

            Assert.Equal(new[] { "Directory", "Sales" }, plan.LabelsToCreate);
            Assert.Empty(plan.LabelsToUpdate);
            Assert.Empty(plan.LabelsToDelete);
        }

        [Fact]
        public void CreatePlan_Deletes_Only_Stale_AppOwned_Labels()
        {
            var reconciler = new GoogleContactGroupLabelReconciler();
            var staleOwned = Label("contactGroups/stale", "Old", "contact-mesh", "Old");
            var personal = Label("contactGroups/personal", "Old", "other-app", "Old");

            var plan = reconciler.CreatePlan(
                "contact-mesh",
                new[] { "Directory" },
                new[] { staleOwned, personal });

            var label = Assert.Single(plan.LabelsToDelete);
            Assert.Equal(staleOwned, label);
            Assert.Equal(new[] { "Directory" }, plan.LabelsToCreate);
        }

        [Fact]
        public void CreatePlan_Updates_Display_Name_And_Tracks_Existing_Resources()
        {
            var reconciler = new GoogleContactGroupLabelReconciler();
            var existing = Label("contactGroups/directory", "directory", "contact-mesh", "directory");

            var plan = reconciler.CreatePlan(
                "contact-mesh",
                new[] { "Directory" },
                new[] { existing });

            var update = Assert.Single(plan.LabelsToUpdate);
            Assert.Equal(existing, update.ExistingLabel);
            Assert.Equal("Directory", update.DesiredName);
            Assert.Empty(plan.LabelsToCreate);
            Assert.Empty(plan.LabelsToDelete);
            Assert.Equal("contactGroups/directory", plan.ResourceNamesByLabel["Directory"]);
        }

        [Fact]
        public void CreateClientData_Stores_App_Ownership_And_Label_Name()
        {
            var clientData = GoogleContactGroupLabelReconciler.CreateClientData("contact-mesh", " Directory ");

            Assert.Equal("contact-mesh", clientData[GoogleContactGroupLabelReconciler.AppIdClientDataKey]);
            Assert.Equal("Directory", clientData[GoogleContactGroupLabelReconciler.LabelNameClientDataKey]);
        }

        private static GoogleContactGroupLabel Label(
            string resourceName,
            string name,
            string appId,
            string labelName)
        {
            return new GoogleContactGroupLabel(
                resourceName,
                name,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [GoogleContactGroupLabelReconciler.AppIdClientDataKey] = appId,
                    [GoogleContactGroupLabelReconciler.LabelNameClientDataKey] = labelName
                });
        }
    }
}
