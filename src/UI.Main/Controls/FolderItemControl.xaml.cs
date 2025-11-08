using System;
using System.IO;
using Fraxiinus.ReplayBook.UI.Main.Models.View;
using Fraxiinus.ReplayBook.UI.Main.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Fraxiinus.ReplayBook.UI.Main.Utilities;

namespace Fraxiinus.ReplayBook.UI.Main.Controls
{
    /// <summary>
    /// Interaction logic for ReplayItem.xaml
    /// </summary>
    public partial class FolderItemControl : UserControl
    {

        private ReplayFolder Context => DataContext as ReplayFolder;    

        private MainWindowViewModel ViewModel => Window.GetWindow(this)?.DataContext as MainWindowViewModel;

        public FolderItemControl()
        {
            InitializeComponent();
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (Context == null) { return; }
            if (sender is not Button moreButton) { return; }

            // Get the button and menu, set placement and open
            ContextMenu contextMenu = moreButton.ContextMenu;
            contextMenu.PlacementTarget = moreButton;
            contextMenu.IsOpen = true;
        }

        private void UIElement_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            DockPanelFolderContextMenu.Placement = PlacementMode.MousePoint;
            DockPanelFolderContextMenu.IsOpen = true;
        }

        private void OpenContainingFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Context != null)
            {
                ViewModel.OpenContainingFolder(Context.Location);
            }
        }

        private void RenameFolder_OnClick(object sender, RoutedEventArgs e)
        {
            if (Context == null) { return; }
            
            var flyout = FlyoutHelper.CreateFlyout(includeButton: true, includeCustom: true);
            flyout.GetFlyoutLabel().Visibility = Visibility.Collapsed;
            flyout.SetFlyoutButtonText(TryFindResource("RenameReplayFile") as string);

            // Create textbox to add as flyout custom element
            var fileNameBox = new TextBox
            {
                Text = Context.Name,
                IsReadOnly = false,
                MinWidth = 200
            };
            Grid.SetColumn(fileNameBox, 0);
            Grid.SetRow(fileNameBox, 1);
            _ = (flyout.Content as Grid).Children.Add(fileNameBox);

            // Handle save button
            flyout.GetFlyoutButton().Click += (object eSender, RoutedEventArgs eConfirm) =>
            {
                // Rename the file and see if an error was returned
                string error = ViewModel.RenameFolder(Context, fileNameBox.Text);

                if (error != null)
                {
                    // Display the error using the label
                    flyout.SetFlyoutLabelText(error.Replace('\n', ' '));
                    flyout.GetFlyoutLabel().Visibility = Visibility.Visible;
                    flyout.GetFlyoutLabel().Foreground = TryFindResource("SystemControlErrorTextForegroundBrush") as Brush;
                    fileNameBox.Margin = new Thickness(0, 0, 0, 0);
                }
                else
                {
                    // Hide the flyout
                    Dispatcher.Invoke(() =>
                    {
                        flyout.Hide();
                    });
                }
            };

            // Show the flyout and focus it
            flyout.ShowAt(FolderNameText);
            fileNameBox.SelectAll();
            _ = fileNameBox.Focus();
        }

        private void DeleteFolder_OnClick(object sender, RoutedEventArgs e)
        {
            if (Context == null) { return; }
            
            // create the flyout
            var flyout = FlyoutHelper.CreateFlyout(includeButton: true, includeCustom: false);

            // set the flyout texts
            flyout.SetFlyoutButtonText(TryFindResource("DeleteReplayFile") as string);
            flyout.SetFlyoutLabelText(TryFindResource("DeleteFlyoutLabel") as string);

            // set button click function
            flyout.GetFlyoutButton().Click += async (object eSender, RoutedEventArgs eConfirm) =>
            {
                await ViewModel.DeleteFolder(Context).ConfigureAwait(false);

                // Hide the flyout
                Dispatcher.Invoke(() =>
                {
                    flyout.Hide();
                });
            };

            // Show the flyout and focus it
            flyout.ShowAt(FolderNameText);
            _ = flyout.GetFlyoutButton().Focus();
        }

        private async void RefreshList_Click(object sender, RoutedEventArgs e)
        {
            if (Context != null)
            {
                await ViewModel.ReloadReplayList(true).ConfigureAwait(true);
            }
        }

        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            if (Context != null)
            {
                Context.IsHovered = true;
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Context != null)
            {
                Context.IsHovered = false;
            }
        }
    }
}
