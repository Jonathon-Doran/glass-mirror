using Glass.Core;
using Glass.Data.Models;
using Glass.Data.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Glass;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ManageVideoSourcesDialog
//
// Dialog for creating, editing, and deleting video source subsample regions.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public partial class ManageVideoSourcesDialog : Window
{
    private List<VideoSource> _sources = new();
    private VideoSource? _selectedSource = null;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ManageVideoSourcesDialog
    //
    // Constructor. Loads all video sources from the database.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ManageVideoSourcesDialog()
    {
        InitializeComponent();
        LoadSources();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // LoadSources
    //
    // Loads all video sources from the database and populates the list view.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void LoadSources()
    {
        DebugLog.Write("ManageVideoSourcesDialog.LoadSources: loading sources.");

        VideoSourceRepository repo = new VideoSourceRepository();
        _sources = repo.GetAll().ToList();

        SourceListView.ItemsSource = _sources;

        DebugLog.Write($"ManageVideoSourcesDialog.LoadSources: loaded {_sources.Count} sources.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // SourceListView_KeyDown
    //
    // Clears selection when ESC is pressed in the list.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void SourceListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DebugLog.Write("ManageVideoSourcesDialog.SourceListView_KeyDown: ESC pressed, clearing selection.");
            ClearSelection();
            e.Handled = true;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // SourceListView_SelectionChanged
    //
    // Fires when the user selects a source in the list. Loads the source into the edit controls.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void SourceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceListView.SelectedItem is not VideoSource source)
        {
            DebugLog.Write("ManageVideoSourcesDialog.SourceListView_SelectionChanged: no source selected.");
            ClearSelection();
            return;
        }

        DebugLog.Write($"ManageVideoSourcesDialog.SourceListView_SelectionChanged: source='{source.Name}'.");

        _selectedSource = source;
        NameTextBox.Text = source.Name;
        XTextBox.Text = source.X.ToString();
        YTextBox.Text = source.Y.ToString();
        WidthTextBox.Text = source.Width.ToString();
        HeightTextBox.Text = source.Height.ToString();

        NewUpdateButton.Content = "Update";
        NewUpdateButton.IsEnabled = true;
        DeleteButton.IsEnabled = true;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ClearSelection
    //
    // Clears the selected source and edit controls.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void ClearSelection()
    {
        DebugLog.Write("ManageVideoSourcesDialog.ClearSelection: clearing selection.");

        _selectedSource = null;
        SourceListView.SelectedItem = null;
        NameTextBox.Text = string.Empty;
        XTextBox.Text = string.Empty;
        YTextBox.Text = string.Empty;
        WidthTextBox.Text = string.Empty;
        HeightTextBox.Text = string.Empty;

        NewUpdateButton.Content = "New";
        NewUpdateButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // NameTextBox_TextChanged
    //
    // Fires when the name text changes. Enables the New/Update button if name is not empty.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool hasName = !string.IsNullOrWhiteSpace(NameTextBox.Text);
        NewUpdateButton.IsEnabled = hasName;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // NewUpdateButton_Click
    //
    // Creates a new source or updates the selected source, saving immediately to the database.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void NewUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            DebugLog.Write("ManageVideoSourcesDialog.NewUpdateButton_Click: name is empty.");
            MessageBox.Show("Please enter a name.", "Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(XTextBox.Text, out int x))
        {
            DebugLog.Write("ManageVideoSourcesDialog.NewUpdateButton_Click: invalid X value.");
            MessageBox.Show("Please enter a valid X coordinate.", "Invalid X", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(YTextBox.Text, out int y))
        {
            DebugLog.Write("ManageVideoSourcesDialog.NewUpdateButton_Click: invalid Y value.");
            MessageBox.Show("Please enter a valid Y coordinate.", "Invalid Y", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(WidthTextBox.Text, out int width))
        {
            DebugLog.Write("ManageVideoSourcesDialog.NewUpdateButton_Click: invalid Width value.");
            MessageBox.Show("Please enter a valid width.", "Invalid Width", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(HeightTextBox.Text, out int height))
        {
            DebugLog.Write("ManageVideoSourcesDialog.NewUpdateButton_Click: invalid Height value.");
            MessageBox.Show("Please enter a valid height.", "Invalid Height", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        VideoSourceRepository repo = new VideoSourceRepository();

        if (_selectedSource != null)
        {
            DebugLog.Write($"ManageVideoSourcesDialog.NewUpdateButton_Click: updating source id={_selectedSource.Id}.");
            _selectedSource.Name = name;
            _selectedSource.X = x;
            _selectedSource.Y = y;
            _selectedSource.Width = width;
            _selectedSource.Height = height;
            repo.Save(_selectedSource);
            DebugLog.Write($"ManageVideoSourcesDialog.NewUpdateButton_Click: saved source id={_selectedSource.Id}.");
        }
        else
        {
            DebugLog.Write("ManageVideoSourcesDialog.NewUpdateButton_Click: creating new source.");
            VideoSource newSource = new VideoSource
            {
                Name = name,
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
            repo.Save(newSource);
            DebugLog.Write($"ManageVideoSourcesDialog.NewUpdateButton_Click: saved new source id={newSource.Id}.");
            _sources.Add(newSource);
        }

        LoadSources();
        ClearSelection();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DeleteButton_Click
    //
    // Deletes the selected source after confirmation, immediately removing from database.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSource == null)
        {
            DebugLog.Write("ManageVideoSourcesDialog.DeleteButton_Click: no source selected.");
            return;
        }

        DebugLog.Write($"ManageVideoSourcesDialog.DeleteButton_Click: confirming delete of '{_selectedSource.Name}'.");

        MessageBoxResult result = MessageBox.Show(
            $"Delete video source '{_selectedSource.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            DebugLog.Write($"ManageVideoSourcesDialog.DeleteButton_Click: deleting source id={_selectedSource.Id}.");

            VideoSourceRepository repo = new VideoSourceRepository();
            repo.Delete(_selectedSource.Id);

            _sources.Remove(_selectedSource);
            LoadSources();
            ClearSelection();
        }
        else
        {
            DebugLog.Write("ManageVideoSourcesDialog.DeleteButton_Click: delete cancelled.");
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cancel_Click
    //
    // Closes the dialog.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DebugLog.Write("ManageVideoSourcesDialog.Cancel_Click: closing dialog.");
        Close();
    }
}