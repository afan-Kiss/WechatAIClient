using System.Collections.ObjectModel;
using Avalonia.Threading;
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
    private CancellationTokenSource? _searchCts;
    private int _searchVersion;
    private CancellationTokenSource? _initCts;
    private int _initVersion;
    private EventHandler<MessageReceivedEventArgs>? _messageReceivedHandler;

    public ContactListViewModel(IWechatService wechatService, ILogger<ContactListViewModel> logger)
    {
        _wechatService = wechatService;
        _logger = logger;
        _messageReceivedHandler = OnMessageReceived;
        _wechatService.MessageReceived += _messageReceivedHandler;
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

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _initCts?.Cancel();
        _initCts?.Dispose();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _initCts = cts;
        var version = Interlocked.Increment(ref _initVersion);

        try
        {
            var recent = (await _wechatService.GetRecentChatsAsync(cts.Token)).ToList();
            var friends = (await _wechatService.GetContactsAsync(cts.Token)).ToList();
            var groups = (await _wechatService.GetGroupsAsync(cts.Token)).ToList();

            if (version != _initVersion || cts.IsCancellationRequested)
            {
                return;
            }

            _allRecent = recent;
            _friends = friends;
            _groups = groups;
            RefreshVisible();
            if (VisibleContacts.Count > 0)
            {
                SelectedContact = VisibleContacts[0];
            }
            else
            {
                SelectedContact = null;
            }
        }
        catch (OperationCanceledException)
        {
            // superseded initialize
        }
    }

    public Contact? FindContact(string contactId)
        => FindContact(accountId: null, contactId);

    public Contact? FindContact(string? accountId, string contactId)
    {
        bool Match(Contact c) =>
            string.Equals(c.Id, contactId, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(accountId) ||
             string.Equals(c.AccountId, accountId, StringComparison.Ordinal));

        return _allRecent.FirstOrDefault(Match)
               ?? _friends.FirstOrDefault(Match)
               ?? _groups.FirstOrDefault(Match)
               ?? VisibleContacts.FirstOrDefault(Match);
    }

    public void BumpRecent(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);

        _allRecent.RemoveAll(c =>
            c.Id == contact.Id &&
            string.Equals(c.AccountId, contact.AccountId, StringComparison.Ordinal));
        _allRecent.Insert(0, contact);
        _allRecent = _allRecent
            .OrderByDescending(c => c.LastMessageTime)
            .ToList();

        if (SelectedTabIndex == 0 && string.IsNullOrWhiteSpace(SearchText))
        {
            RefreshVisible();
        }
    }

    public void ClearUnread(Contact contact)
    {
        if (contact is null)
        {
            return;
        }

        contact.UnreadCount = 0;
    }

    public void Cleanup()
    {
        if (_messageReceivedHandler is not null)
        {
            _wechatService.MessageReceived -= _messageReceivedHandler;
            _messageReceivedHandler = null;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        _initCts?.Cancel();
        _initCts?.Dispose();
        _initCts = null;
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        void Apply()
        {
            var contact = FindContact(e.AccountId, e.ContactId);
            if (contact is null)
            {
                return;
            }

            // LastMessage fields are already updated by the wechat service on the shared instance.
            if ((SelectedContact?.Id != contact.Id ||
                 !string.Equals(SelectedContact?.AccountId, contact.AccountId, StringComparison.Ordinal)) &&
                !e.Message.IsSelf)
            {
                contact.UnreadCount++;
            }

            BumpRecent(contact);
        }

        // Unit tests (no Avalonia lifetime): apply immediately so assertions are deterministic.
        if (Avalonia.Application.Current is null)
        {
            Apply();
            return;
        }

        Dispatcher.UIThread.Post(Apply);
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var version = Interlocked.Increment(ref _searchVersion);
        _ = DebouncedSearchAsync(value, version, cts.Token);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            _ = RunSearchAsync(SearchText, Interlocked.Increment(ref _searchVersion), CancellationToken.None);
        }
        else
        {
            RefreshVisible();
        }
    }

    partial void OnSelectedContactChanged(Contact? value)
    {
        if (value is null)
        {
            return;
        }

        ClearUnread(value);
        ContactSelected?.Invoke(this, value);
    }

    [RelayCommand]
    private void SelectTab(string indexText)
    {
        if (int.TryParse(indexText, out var index))
        {
            SelectedTabIndex = index;
        }
    }

    private async Task DebouncedSearchAsync(string keyword, int version, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
            if (version != _searchVersion)
            {
                return;
            }

            await RunSearchAsync(keyword, version, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // debounce superseded
        }
    }

    private async Task RunSearchAsync(string keyword, int version, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                if (version == _searchVersion)
                {
                    RefreshVisible();
                }

                return;
            }

            var tabFilter = SelectedTabIndex switch
            {
                1 => (ContactType?)ContactType.Friend,
                2 => ContactType.Group,
                _ => null
            };

            var results = await _wechatService.SearchAsync(keyword, tabFilter, cancellationToken);
            if (version != _searchVersion)
            {
                return;
            }

            VisibleContacts.Clear();
            foreach (var hit in results)
            {
                VisibleContacts.Add(hit.Contact);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
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
