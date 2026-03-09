using System;

// Model pozycji w koszyku (tylko dla WPF)
public class BasketItem : System.ComponentModel.INotifyPropertyChanged
{
    public string Barcode { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }

    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set { _quantity = value; OnPropertyChanged("Total"); OnPropertyChanged("Quantity"); }
    }
    public decimal Total => Price * Quantity;

    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
