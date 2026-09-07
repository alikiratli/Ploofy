using System.Collections;
using System.Collections.Specialized;

namespace Ploofy.Ui.Controls;

/// <summary>
/// Sabit sayıda parçadan oluşan bir oyun tahtası: eldeki alanı satır ve
/// sütunlara eşit bölüp parçaları oraya oturtur.
/// </summary>
/// <remarks>
/// <para>
/// Bu tahtalar bir süre <c>CollectionView</c> ile ve parça başına sabit
/// piksel boyuyla çizildi (kart 140, tuş 150, kutucuk 160). Yatay tablette
/// sığıyordu; kısa bir ekranda — telefon yatayken yaklaşık 411 birim —
/// sığmıyor ve tahtanın yarısı ekranın altında kalıyordu. Çocuk kaydırmayı
/// bilmiyor: onun gördüğü şey oyunun eksik olması.
/// </para>
/// <para>
/// Boyu hesaplayıp <c>HeightRequest</c>'e bağlamak da denendi ve tutmadı;
/// liste parçaları ilk ölçümden sonra yeniden ölçmüyor. Asıl mesele şu:
/// <b>bunlar liste değil, tahta.</b> Kaydırma istemiyorlar, öğe geri
/// dönüşümü istemiyorlar, hepsi aynı anda görünmek zorunda. Doğru kap bir
/// <see cref="Grid"/>: hücreler <c>*</c> olduğunda parçalar eldeki alanı
/// kendiliğinden paylaşıyor ve hiçbir yerde aritmetik kalmıyor.
/// </para>
/// <para>
/// Parçanın tasarım boyu <c>MaximumHeightRequest</c> ile korunuyor: geniş
/// ekranda kart eskisi gibi duruyor, dar ekranda küçülüyor.
/// </para>
/// </remarks>
public sealed class BoardView : Grid
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(BoardView),
        propertyChanged: OnItemsSourceChanged);

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(BoardView),
        propertyChanged: OnLayoutChanged);

    public static readonly BindableProperty ColumnsProperty = BindableProperty.Create(
        nameof(Columns), typeof(int), typeof(BoardView), 1,
        propertyChanged: OnLayoutChanged);

    private INotifyCollectionChanged? _watched;

    public BoardView()
    {
        // Kabın ölçüsü sütun kararına giriyor: dar ve uzun bir alanda üç
        // sütun doğru, geniş ve basık bir alanda değil.
        SizeChanged += (_, _) => Rebuild();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Kaç sütun. Satır sayısı parça sayısından çıkıyor.</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private static void OnLayoutChanged(BindableObject bindable, object? old, object? value) =>
        ((BoardView)bindable).Rebuild();

    private static void OnItemsSourceChanged(BindableObject bindable, object? old, object? value)
    {
        var board = (BoardView)bindable;

        // Tur değiştiğinde koleksiyon yeniden doluyor; tahtanın da yeniden
        // kurulması gerekiyor.
        if (board._watched is not null)
        {
            board._watched.CollectionChanged -= board.OnItemsChanged;
        }

        board._watched = value as INotifyCollectionChanged;

        if (board._watched is not null)
        {
            board._watched.CollectionChanged += board.OnItemsChanged;
        }

        board.Rebuild();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <summary>
    /// Hücrenin bundan daha yassı olmasına izin verilmiyor (genişlik / yükseklik).
    /// </summary>
    /// <remarks>
    /// Yassı hücre yalnızca çirkin değil, <b>okunmaz</b>: Harf Avı'nın
    /// kutucukları kısa bir ekranda 291×73 birime düşünce harfler tümüyle
    /// kayboldu, geriye renkli çubuklar kaldı. Eşik üçte: tabletteki
    /// tasarımlar (Eşleştirme 2,9, Sırayı Tekrarla 2,8) bunun altında kalıyor,
    /// yani geniş ekranda hiçbir şey değişmiyor.
    /// </remarks>
    private const double MaxCellAspect = 3.0;

    /// <summary>
    /// Kaç sütun kullanılacağı: istenen sayı, hücreyi okunmaz yassılıkta
    /// bırakmıyorsa aynen; bırakıyorsa satırı azaltan bir üst bölen.
    /// </summary>
    /// <remarks>
    /// Aday sütunlar <b>bölenler</b>: 20 parçayı 7 sütuna dizmek son satırı
    /// eksik bırakıyor ve tahta yamuk görünüyor. Hiçbir bölen yetmezse tek
    /// satır (parça sayısı kadar sütun) kalıyor — o hep sığar.
    /// </remarks>
    private int ChooseColumns(int count)
    {
        var wanted = Math.Clamp(Columns, 1, count);

        if (Width <= 0 || Height <= 0)
        {
            return wanted;
        }

        for (var columns = wanted; columns <= count; columns++)
        {
            if (count % columns != 0 && columns != count)
            {
                continue;
            }

            var rows = (int)Math.Ceiling(count / (double)columns);
            var cell = (Width / columns) / (Height / rows);

            if (cell <= MaxCellAspect)
            {
                return columns;
            }
        }

        return count;
    }

    private void Rebuild()
    {
        Children.Clear();
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();

        if (ItemsSource is null || ItemTemplate is null || Columns <= 0)
        {
            return;
        }

        var items = ItemsSource.Cast<object>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        var columns = ChooseColumns(items.Count);
        var rows = (int)Math.Ceiling(items.Count / (double)columns);

        for (var r = 0; r < rows; r++)
        {
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        }

        for (var c = 0; c < columns; c++)
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        }

        for (var i = 0; i < items.Count; i++)
        {
            // Şablon bir DataTemplateSelector olabilir; SelectTemplate ikisini
            // de doğru çözüyor.
            var template = ItemTemplate is DataTemplateSelector selector
                ? selector.SelectTemplate(items[i], this)
                : ItemTemplate;

            if (template.CreateContent() is not View view)
            {
                continue;
            }

            view.BindingContext = items[i];
            SetColumn((BindableObject)view, i % columns);
            SetRow((BindableObject)view, i / columns);
            Children.Add(view);
        }
    }
}
