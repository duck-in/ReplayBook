using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public abstract class ReplayNode : INotifyPropertyChanged
{
    private bool _isHovered;
    
    public bool IsHovered
    {
        get => _isHovered;
        set => SetField(ref _isHovered, value);
    }

    
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((ReplayNode)obj);
    }

    protected bool Equals(ReplayNode other)
    {
        return Location == other.Location;
    }

    public override int GetHashCode()
    {
        return (Location != null ? Location.GetHashCode() : 0);
    }

    public string Location { get; set; }
}