import React, { useState } from 'react';
import { Database, Shield, Server, Users, FileText, Settings, Key, CheckCircle, RefreshCw } from 'lucide-react';

export default function AdminPortal() {
  const [activeTab, setActiveTab] = useState<'databases' | 'geoserver' | 'users' | 'audit'>('databases');

  const [dbConnections, setDbConnections] = useState([
    { id: '1', name: 'Oracle Telecom GIS', provider: 'Oracle', host: 'localhost:1521/XEPDB1', schema: 'PPGIS', active: true },
    { id: '2', name: 'PostgreSQL PostGIS', provider: 'PostgreSQL', host: 'localhost:5432/gisdb', schema: 'public', active: true },
    { id: '3', name: 'MS SQL Server GIS', provider: 'SqlServer', host: 'localhost:1433/GISDB', schema: 'dbo', active: false },
    { id: '4', name: 'MySQL Spatial DB', provider: 'MySQL', host: 'localhost:3306/spatial', schema: 'spatial', active: true }
  ]);

  const [geoServerConfig, setGeoServerConfig] = useState({
    url: 'http://localhost:8080/geoserver',
    username: 'admin',
    password: '••••••••'
  });

  const [auditLogs, setAuditLogs] = useState([
    { id: '1', user: 'analyst', action: 'SPATIAL_EXTRACTION', table: 'ODN_CONT_GEOM', records: 23421, format: 'GeoJSON', time: '2026-08-28 10:15:00', duration: '12ms' },
    { id: '2', user: 'admin', action: 'DISCOVER_LAYERS', table: 'ALL_LAYERS', records: 3, format: 'N/A', time: '2026-08-28 09:30:00', duration: '45ms' }
  ]);

  return (
    <div className="max-w-7xl mx-auto space-y-6">
      {/* Admin Title */}
      <div className="bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl flex items-center justify-between">
        <div>
          <h2 className="text-xl font-bold text-slate-100 flex items-center space-x-2">
            <Shield className="w-6 h-6 text-emerald-400" />
            <span>Administrator Portal & System Management</span>
          </h2>
          <p className="text-xs text-slate-400 mt-1">
            Configure database connections, GeoServer REST settings, user roles, layer security permissions, and audit logs.
          </p>
        </div>
      </div>

      {/* Admin Sub Navigation Tabs */}
      <div className="flex bg-slate-900 p-1.5 rounded-xl border border-slate-800 w-fit">
        <button
          onClick={() => setActiveTab('databases')}
          className={`flex items-center space-x-2 px-4 py-2 rounded-lg text-xs font-semibold transition ${
            activeTab === 'databases' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <Database className="w-4 h-4" />
          <span>Database Management</span>
        </button>
        <button
          onClick={() => setActiveTab('geoserver')}
          className={`flex items-center space-x-2 px-4 py-2 rounded-lg text-xs font-semibold transition ${
            activeTab === 'geoserver' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <Server className="w-4 h-4" />
          <span>GeoServer Config</span>
        </button>
        <button
          onClick={() => setActiveTab('users')}
          className={`flex items-center space-x-2 px-4 py-2 rounded-lg text-xs font-semibold transition ${
            activeTab === 'users' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <Users className="w-4 h-4" />
          <span>Users & Layer Permissions</span>
        </button>
        <button
          onClick={() => setActiveTab('audit')}
          className={`flex items-center space-x-2 px-4 py-2 rounded-lg text-xs font-semibold transition ${
            activeTab === 'audit' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
          }`}
        >
          <FileText className="w-4 h-4" />
          <span>Audit Logs</span>
        </button>
      </div>

      {/* Database Config Tab */}
      {activeTab === 'databases' && (
        <div className="bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl space-y-4">
          <div className="flex justify-between items-center border-b border-slate-800 pb-3">
            <h3 className="text-sm font-bold text-slate-200">Configured Multi-Database Engine Connections</h3>
            <button className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs px-3 py-1.5 rounded-lg font-medium">
              + Add Database
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {dbConnections.map(db => (
              <div key={db.id} className="bg-slate-950 border border-slate-800 p-4 rounded-xl flex justify-between items-start">
                <div>
                  <div className="flex items-center space-x-2">
                    <span className="text-xs font-bold text-slate-100">{db.name}</span>
                    <span className="text-[10px] bg-slate-800 text-emerald-400 font-mono px-2 py-0.5 rounded">{db.provider}</span>
                  </div>
                  <p className="text-xs text-slate-400 font-mono mt-1">{db.host}</p>
                  <span className="text-[10px] text-slate-500">Schema: {db.schema}</span>
                </div>
                <div className="flex flex-col items-end space-y-2">
                  <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${db.active ? 'bg-emerald-950 text-emerald-400 border border-emerald-800' : 'bg-slate-800 text-slate-500'}`}>
                    {db.active ? 'Active' : 'Disabled'}
                  </span>
                  <button className="text-xs text-emerald-400 hover:underline">Test Connection</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* GeoServer Tab */}
      {activeTab === 'geoserver' && (
        <div className="bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl space-y-5 max-w-2xl">
          <h3 className="text-sm font-bold text-slate-200 border-b border-slate-800 pb-3">GeoServer Connection Settings</h3>
          <div className="space-y-3">
            <div>
              <label className="text-xs font-medium text-slate-300 block mb-1">GeoServer REST / Endpoint URL</label>
              <input
                type="text"
                value={geoServerConfig.url}
                onChange={(e) => setGeoServerConfig({ ...geoServerConfig, url: e.target.value })}
                className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200 font-mono"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-xs font-medium text-slate-300 block mb-1">Admin Username</label>
                <input
                  type="text"
                  value={geoServerConfig.username}
                  onChange={(e) => setGeoServerConfig({ ...geoServerConfig, username: e.target.value })}
                  className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200 font-mono"
                />
              </div>
              <div>
                <label className="text-xs font-medium text-slate-300 block mb-1">Admin Password</label>
                <input
                  type="password"
                  value={geoServerConfig.password}
                  onChange={(e) => setGeoServerConfig({ ...geoServerConfig, password: e.target.value })}
                  className="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200 font-mono"
                />
              </div>
            </div>
            <div className="flex space-x-3 pt-2">
              <button className="bg-emerald-600 hover:bg-emerald-500 text-white text-xs px-4 py-2 rounded-xl font-medium">
                Save & Test GeoServer
              </button>
              <button className="bg-slate-800 hover:bg-slate-700 text-emerald-400 text-xs px-4 py-2 rounded-xl font-medium">
                Discover Layers
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Users & Permissions Tab */}
      {activeTab === 'users' && (
        <div className="bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl space-y-4">
          <h3 className="text-sm font-bold text-slate-200 border-b border-slate-800 pb-3">Role-Based Access Control & Granular Layer Permissions</h3>
          <div className="text-xs text-slate-400">
            Granular access controls enforced at backend level: VIEW, QUERY, EXTRACT, SPATIAL_ANALYSIS, DOWNLOAD.
          </div>
          <div className="bg-slate-950 p-4 rounded-xl border border-slate-800 space-y-3">
            <div className="flex justify-between items-center text-xs text-slate-200 font-semibold">
              <span>Role: GIS_ANALYST</span>
              <span className="text-emerald-400 font-mono">Assigned Users: 8</span>
            </div>
            <div className="text-xs text-slate-400 space-y-1">
              <div>• ODN_CONT_GEOM: VIEW (✓), QUERY (✓), EXTRACT (✓), SPATIAL_ANALYSIS (✓), DOWNLOAD (✓)</div>
              <div>• LOTS_GEOM: VIEW (✓), QUERY (✓), EXTRACT (✕), SPATIAL_ANALYSIS (✕), DOWNLOAD (✕)</div>
            </div>
          </div>
        </div>
      )}

      {/* Audit Logs Tab */}
      {activeTab === 'audit' && (
        <div className="bg-slate-900 border border-slate-800 p-6 rounded-2xl shadow-xl space-y-4">
          <h3 className="text-sm font-bold text-slate-200 border-b border-slate-800 pb-3">System Audit Logging</h3>
          <div className="overflow-x-auto rounded-xl border border-slate-800">
            <table className="w-full text-left text-xs text-slate-300">
              <thead className="bg-slate-950 text-slate-400 border-b border-slate-800">
                <tr>
                  <th className="p-3 font-semibold">User</th>
                  <th className="p-3 font-semibold">Action</th>
                  <th className="p-3 font-semibold">Target Table</th>
                  <th className="p-3 font-semibold">Records</th>
                  <th className="p-3 font-semibold">Format</th>
                  <th className="p-3 font-semibold">Timestamp</th>
                  <th className="p-3 font-semibold">Execution Time</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60 bg-slate-900/50">
                {auditLogs.map(log => (
                  <tr key={log.id} className="hover:bg-slate-800/50 transition">
                    <td className="p-3 font-mono text-emerald-400">{log.user}</td>
                    <td className="p-3 font-medium text-slate-200">{log.action}</td>
                    <td className="p-3 font-mono">{log.table}</td>
                    <td className="p-3">{log.records}</td>
                    <td className="p-3">{log.format}</td>
                    <td className="p-3 text-slate-400">{log.time}</td>
                    <td className="p-3 font-mono text-emerald-400">{log.duration}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
