using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class SafeCallback
{

    private readonly object _lock = new object();
    public delegate void SafeCallbackType();
    private event SafeCallbackType onEvent;

    public static SafeCallback operator +(SafeCallback a, SafeCallbackType b)
    {
        lock (a._lock)
        {
            a.onEvent += b;
        }
        return a;
    }
    public void Invoke()
    {
        lock (_lock)
        {
            onEvent?.Invoke();
        }
    }
}

    public abstract class ConnectionProviderBase : IDisposable
{
    public delegate void ConnectedCallback();
    public delegate void DisconnectedCallback();

    public event ConnectedCallback OnConnected;
    public event DisconnectedCallback OnDisconnected;
    private readonly object _lock = new();
    protected abstract string NAME { get; }
    public bool IsConnected { get; protected set; }
    private bool disposedValue;
    public readonly string ID;
    public ConnectionProviderBase(LocalConnectedClient localClient, ClientAddedToProviderMessage pMsg)
    {
        this.ID = pMsg.providerKey;
    }

    public void Connect()
    {
        Logger.LogStatus(NAME, Logger.Status.ProviderConnectionStart);
        connectInternal();
        if (IsConnected)
        {
            Logger.LogStatus(NAME, Logger.Status.ProviderConnectionSuccess);
            onConnectionSucces();
        }
        else
        {
            Logger.LogStatus(NAME, Logger.Status.ProviderConnectionFailed);
        }
    }
    public async Task ConnectAsync()
    {
        //Debug.Log($"Connecting to {NAME} with ID {ID}");
        Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderConnectionStart, $"mode=async");
        await Task.Run(() =>
        {
            Connect();
        });
    }
    protected abstract void connectInternal();
    public void Disconnect()
    {
        if(IsConnected)
        {
            disconnectInternal();
            onConnectionClosed();
            IsConnected = false;
        }
    }
    public async Task DisconnectAsync()
    {
        await Task.Run(() =>
        {
            Disconnect();
        });
    }
    protected abstract void disconnectInternal();
    public abstract bool IsReady();
    public void AddOnConnectedCallback(ConnectedCallback cb, bool callIfAlreadyConnected = true)
    {
        bool isConnectedCopy;
        lock (_lock)
        {
            isConnectedCopy = IsConnected;
            OnConnected += cb;
        }
        if (isConnectedCopy)
        {
            cb?.Invoke();
        }
    }
    public void AddOnDisconnectedCallback(DisconnectedCallback cb, bool callIfAlreadyDisconnected = true)
    {
        bool isConnectedCopy;
        lock(_lock)
        {
            isConnectedCopy = IsConnected;
            OnDisconnected += cb;
        }
        if(!isConnectedCopy)
        {
            cb?.Invoke();
        }
    }
    protected void onConnectionSucces()
    {
        lock(_lock)
        {
            OnConnected?.Invoke();
        }
        
    }
    protected void onConnectionClosed()
    {
        ConnectionProviderRepository.RemoveProvider(ID);
        lock(_lock)
        {
            OnDisconnected?.Invoke();
        }
        
    }
    
    protected abstract void Dispose(bool disposing);
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Logger.LogStatus(NAME, Logger.Status.Disposing);
        if(IsConnected)
        {
            Disconnect();
        }
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
        Logger.LogStatus(NAME, Logger.Status.Disposed);
    }
}
