using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.ViewModels;

public partial class ContactListViewModel : ViewModelBase
{
    private readonly IWechatService _wechatService;
    private readonly ILogger<ContactListViewModel> _logger;
    private List<Contact> _allRecent = [];
    private List<Contact> _friends = [];
    private List<Contact> _groups = [];

    public ContactListViewModel(IWechatService wechatService, ILogger<ContactListViewModel> logger)
    {
        _wechatService = wechatService;
        _logger = logger;
    }

    public ObservableCollection<Contact> VisibleContacts { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecentTab))]
    [NotifyPropertyChangedFor(nameof(IsFriendsTab))]
    [NotifyPropertyChangedFor(nameof(IsGroupsTab))]
    private int _selectedTabIndex;

    [ObservableProperty]
    private Contact? _selectedContact;

    public bool IsRecentTab => SelectedTabIndex == 0;
    public bool IsFriendsTab => SelectedTabIndex == 1;
    public bool IsGroupsTab => SelectedTabIndex == 2;

    public event EventHandler<Contact>? ContactSelected;

    public async Task InitializeAsync()
    {
        _allRecent = (await _wechatService.GetRecentChatsAsync()).ToList();
        _friends = (await _wechatService.GetContactsAsync()).ToList();
        _groups = (await _wechatService.GetGroupsAsync()).ToList();
        RefreshVisible();
        if (VisibleContacts.Count > 0)
        {
            SelectedContact = VisibleContacts[0];
        }
    }

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(value);

    partial void OnSelectedTabIndexChanged(int value) => RefreshVisible();

    partial void OnSelectedContactChanged(Contact? value)
    {
        if (value is not null)
        {
            ContactSelected?.Invoke(this, value);
        }
    }

    [RelayCommand]
    private void SelectTab(string indexText)
    {
        if (int.TryParse(indexText, out var index))
        {
            SelectedTabIndex = index;
        }
    }

    private async Task SearchAsync(string keyword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                RefreshVisible();
                return;
            }

            var results = await _wechatService.SearchAsync(keyword);
            VisibleContacts.Clear();
            foreach (var item in results)
            {
                VisibleContacts.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
        }
    }

    private void RefreshVisible()
    {
        IEnumerable<Contact> source = SelectedTabIndex switch
        {
            1 => _friends,
            2 => _groups,
            _ => _allRecent
        };

        VisibleContacts.Clear();
        foreach (var item in source)
        {
            VisibleContacts.Add(item);
        }
    }
}
