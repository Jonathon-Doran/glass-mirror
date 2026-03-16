using Glass.Data.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Glass.Controls;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// RelayGroupMatrix
//
// Displays a scrollable matrix of relay groups (rows) and characters (columns).
// Each cell shows a green checkmark if the character is a member of the group,
// or a red X if not. Clicking a cell toggles membership and raises MembershipChanged.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public partial class RelayGroupMatrix : UserControl
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MembershipChangedEventArgs
    //
    // Carries the details of a relay group membership toggle.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class MembershipChangedEventArgs : EventArgs
    {
        public int GroupId { get; init; }
        public int CharacterId { get; init; }
        public bool Added { get; init; }
    }

    public event EventHandler<MembershipChangedEventArgs>? MembershipChanged;

    private List<Character> _characters = new();
    private List<RelayGroup> _groups = new();

    // Tracks current membership state: key is (groupId, characterId)
    private HashSet<(int GroupId, int CharacterId)> _membership = new();


    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // RelayGroupMatrix
    //
    // Initializes the control and loads the checkmark and X images.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public RelayGroupMatrix()
    {
        InitializeComponent();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Load
    //
    // Populates the matrix with the given groups and characters.
    // membership is the set of (groupId, characterId) pairs that are currently active.
    //
    // groups:      The relay groups to display as rows
    // characters:  The characters to display as columns
    // membership:  The current membership state
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Load(List<RelayGroup> groups, List<Character> characters, HashSet<(int GroupId, int CharacterId)> membership)
    {
        _groups = groups;
        _characters = characters;
        _membership = membership;

        BuildGrid();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // BuildGrid
    //
    // Constructs the matrix grid dynamically based on the current groups and characters.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void BuildGrid()
    {
        MatrixGrid.Children.Clear();
        MatrixGrid.RowDefinitions.Clear();
        MatrixGrid.ColumnDefinitions.Clear();

        // Row 0 is the header row, rows 1..N are groups
        MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var _ in _groups)
        {
            MatrixGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // Column 0 is the group name column, columns 1..N are characters
        MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foreach (var _ in _characters)
        {
            MatrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        // Character name headers (row 0, columns 1..N)
        for (int col = 0; col < _characters.Count; col++)
        {
            var header = new TextBlock
            {
                Text = _characters[col].Name,
                Margin = new Thickness(4, 2, 4, 2),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, col + 1);
            MatrixGrid.Children.Add(header);
        }

        // Group rows
        for (int row = 0; row < _groups.Count; row++)
        {
            var group = _groups[row];

            // Group name header (column 0)
            var groupLabel = new TextBlock
            {
                Text = group.Name,
                Margin = new Thickness(4, 2, 8, 2),
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(groupLabel, row + 1);
            Grid.SetColumn(groupLabel, 0);
            MatrixGrid.Children.Add(groupLabel);

            // Character cells
            for (int col = 0; col < _characters.Count; col++)
            {
                var character = _characters[col];
                bool isMember = _membership.Contains((group.Id, character.Id));

                var cell = BuildCell(group.Id, character.Id, isMember);
                Grid.SetRow(cell, row + 1);
                Grid.SetColumn(cell, col + 1);
                MatrixGrid.Children.Add(cell);
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // BuildCell
    //
    // Creates a single matrix cell button showing the membership state.
    //
    // groupId:      The relay group ID for this cell
    // characterId:  The character ID for this cell
    // isMember:     Whether the character is currently a member of the group
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private Button BuildCell(int groupId, int characterId, bool isMember)
    {
        var text = new TextBlock
        {
            Text = isMember ? "p" : "r",
            FontFamily = isMember ? new FontFamily("Bookshelf Symbol 7") : new FontFamily("Webdings"),
            FontSize = 14,
            Foreground = isMember ? Brushes.Green : Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var button = new Button
        {
            Content = text,
            Margin = new Thickness(2),
            Padding = new Thickness(2),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Tag = (groupId, characterId)
        };

        button.Click += Cell_Click;
        return button;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Cell_Click
    //
    // Handles a cell click. Toggles membership state, updates the cell image,
    // and raises MembershipChanged.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void Cell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var (groupId, characterId) = ((int GroupId, int CharacterId))button.Tag;
        bool isMember = _membership.Contains((groupId, characterId));

        if (isMember)
        {
            _membership.Remove((groupId, characterId));
        }
        else
        {
            _membership.Add((groupId, characterId));
        }

        bool nowMember = !isMember;

        if (button.Content is TextBlock text)
        {
            text.Text = nowMember ? "p" : "r";
            text.FontFamily = nowMember ? new FontFamily("Bookshelf Symbol 7") : new FontFamily("Webdings");
            text.Foreground = nowMember ? Brushes.Green : Brushes.Red;
        }

        MembershipChanged?.Invoke(this, new MembershipChangedEventArgs
        {
            GroupId = groupId,
            CharacterId = characterId,
            Added = nowMember
        });
    }
}