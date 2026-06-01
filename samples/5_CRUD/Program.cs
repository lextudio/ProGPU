using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using ProGPU.Scene;
using ProGPU.Vector;
namespace CrudSample;

public class Person
{
    public string Name { get; set; }
    public string Surname { get; set; }

    public Person(string name, string surname)
    {
        Name = name;
        Surname = surname;
    }

    public string DisplayName => $"{Surname}, {Name}";
}

public class PersonListBoxItem : ListBoxItem
{
    public Person? Person { get; set; }
}

public class Program
{
    public static void Main(string[] args)
    {
        AppBuilder<App>.Configure()
            .WithTitle("7GUI - 5. CRUD (WinUI Application)")
            .WithSize(500, 450)
            .Build()
            .Run(args);
    }
}

public class App : Application
{
    private readonly List<Person> _people = new()
    {
        new Person("Hans", "Emil"),
        new Person("Max", "Mustermann"),
        new Person("Roman", "Tschugger")
    };

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        window.Title = "7GUI - 5. CRUD";
        window.Width = 500;
        window.Height = 450;

        var rootGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var card = new Border
        {
            Background = ThemeManager.GetBrush("CardBackground"),
            BorderBrush = ThemeManager.GetBrush("ControlBorder"),
            BorderThickness = new Thickness(1f),
            CornerRadius = 8f,
            Padding = new Thickness(24),
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.RowDefinitions.Add(new GridLength(45f, GridUnitType.Absolute)); // Filter row
        grid.RowDefinitions.Add(new GridLength(1f, GridUnitType.Star));       // Main row (List + Fields)
        grid.RowDefinitions.Add(new GridLength(45f, GridUnitType.Absolute)); // Action Buttons row

        // Row 0: Filter Prefix
        var filterStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        var filterLabel = new TextBlock
        {
            Text = "Filter prefix:  ",
            FontSize = 14f,
            Foreground = ThemeManager.GetBrush("TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var prefixBox = new TextBox
        {
            FontSize = 14f,
            Width = 140f,
            Height = 32f,
            CornerRadius = 4f
        };
        filterStack.AddChild(filterLabel);
        filterStack.AddChild(prefixBox);
        grid.AddChild(filterStack);
        Grid.SetRow(filterStack, 0);

        // Row 1: Left ListBox, Right Details Form
        var workspaceGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 10, 0, 10)
        };
        workspaceGrid.ColumnDefinitions.Add(new GridLength(1.2f, GridUnitType.Star)); // ListBox Column
        workspaceGrid.ColumnDefinitions.Add(new GridLength(1f, GridUnitType.Star));   // Form Details Column

        // List Box
        var listBox = new ListBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HeightConstraint = 220f,
            Margin = new Thickness(0, 0, 12, 0)
        };
        workspaceGrid.AddChild(listBox);
        Grid.SetColumn(listBox, 0);

        // Details Form
        var detailsForm = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12, 0, 0, 0)
        };

        var nameLabel = new TextBlock
        {
            Text = "Name:",
            FontSize = 12f,
            Foreground = ThemeManager.GetBrush("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var nameBox = new TextBox
        {
            FontSize = 14f,
            Width = 160f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var surnameLabel = new TextBlock
        {
            Text = "Surname:",
            FontSize = 12f,
            Foreground = ThemeManager.GetBrush("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var surnameBox = new TextBox
        {
            FontSize = 14f,
            Width = 160f,
            Height = 32f,
            CornerRadius = 4f
        };

        detailsForm.AddChild(nameLabel);
        detailsForm.AddChild(nameBox);
        detailsForm.AddChild(surnameLabel);
        detailsForm.AddChild(surnameBox);

        workspaceGrid.AddChild(detailsForm);
        Grid.SetColumn(detailsForm, 1);

        grid.AddChild(workspaceGrid);
        Grid.SetRow(workspaceGrid, 1);

        // Row 2: Action Buttons (Create, Update, Delete)
        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var createBtn = new Button
        {
            Content = new TextBlock { Text = "Create", FontSize = 13f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 80f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var updateBtn = new Button
        {
            Content = new TextBlock { Text = "Update", FontSize = 13f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 80f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 8, 0),
            IsEnabled = false
        };

        var deleteBtn = new Button
        {
            Content = new TextBlock { Text = "Delete", FontSize = 13f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 80f,
            Height = 32f,
            CornerRadius = 4f,
            IsEnabled = false
        };

        buttonStack.AddChild(createBtn);
        buttonStack.AddChild(updateBtn);
        buttonStack.AddChild(deleteBtn);
        grid.AddChild(buttonStack);
        Grid.SetRow(buttonStack, 2);

        card.Child = grid;
        rootGrid.AddChild(card);

        window.Content = rootGrid;
        window.Activate();

        // -- REACTIVE CRUD INTERPOLATION ROUTING --

        // Helper to refresh the filtered ListBox
        void RefreshList(Person? selectedToRestore = null)
        {
            string filter = prefixBox.Text.Trim();
            listBox.ClearItems();

            PersonListBoxItem? restoredItem = null;

            foreach (var person in _people)
            {
                if (string.IsNullOrEmpty(filter) || person.Surname.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                {
                    var item = new PersonListBoxItem
                    {
                        Text = person.DisplayName,
                        Person = person
                    };

                    listBox.AddItem(item);

                    if (selectedToRestore != null && person == selectedToRestore)
                    {
                        restoredItem = item;
                    }
                }
            }

            if (restoredItem != null)
            {
                listBox.SelectedItem = restoredItem;
            }
            else
            {
                listBox.SelectedItem = null;
            }
        }

        // 1. Reactive prefix filter TextChanged
        Observable.FromEventPattern(h => prefixBox.TextChanged += h, h => prefixBox.TextChanged -= h)
            .Subscribe(_ =>
            {
                RefreshList();
            });

        // 2. SelectionChanged logic
        Observable.FromEventPattern(h => listBox.SelectionChanged += h, h => listBox.SelectionChanged -= h)
            .Subscribe(_ =>
            {
                var selected = listBox.SelectedItem as PersonListBoxItem;
                if (selected != null && selected.Person != null)
                {
                    nameBox.Text = selected.Person.Name;
                    surnameBox.Text = selected.Person.Surname;

                    updateBtn.IsEnabled = true;
                    deleteBtn.IsEnabled = true;
                }
                else
                {
                    nameBox.Text = "";
                    surnameBox.Text = "";

                    updateBtn.IsEnabled = false;
                    deleteBtn.IsEnabled = false;
                }
            });

        // 3. Create Button Clicked
        Observable.FromEventPattern(h => createBtn.Click += h, h => createBtn.Click -= h)
            .Subscribe(_ =>
            {
                string name = nameBox.Text.Trim();
                string surname = surnameBox.Text.Trim();

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(surname))
                {
                    var newPerson = new Person(name, surname);
                    _people.Add(newPerson);
                    RefreshList(newPerson);
                }
            });

        // 4. Update Button Clicked
        Observable.FromEventPattern(h => updateBtn.Click += h, h => updateBtn.Click -= h)
            .Subscribe(_ =>
            {
                var selected = listBox.SelectedItem as PersonListBoxItem;
                if (selected != null && selected.Person != null)
                {
                    string name = nameBox.Text.Trim();
                    string surname = surnameBox.Text.Trim();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(surname))
                    {
                        selected.Person.Name = name;
                        selected.Person.Surname = surname;
                        RefreshList(selected.Person);
                    }
                }
            });

        // 5. Delete Button Clicked
        Observable.FromEventPattern(h => deleteBtn.Click += h, h => deleteBtn.Click -= h)
            .Subscribe(_ =>
            {
                var selected = listBox.SelectedItem as PersonListBoxItem;
                if (selected != null && selected.Person != null)
                {
                    _people.Remove(selected.Person);
                    RefreshList(null);
                }
            });

        // Initial List Population
        RefreshList();
    }
}
