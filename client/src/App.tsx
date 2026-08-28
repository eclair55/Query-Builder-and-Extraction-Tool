import React, { useState } from 'react';
import { Database, Layers, Map as MapIcon, Filter, Download, Shield, Settings, History, HelpCircle } from 'lucide-react';
import MapComponent from './map/MapComponent';
import LayerPanel from './layers/LayerPanel';
import QueryBuilderPanel from './query-builder/QueryBuilderPanel';
import ExtractionPanel from './extraction/ExtractionPanel';
import AdminPortal from './admin/AdminPortal';

export default function App() {
  const [activeTab, setActiveTab] = useState<'map' | 'query' | 'extraction' | 'admin'>('map');
  const [selectedLayer, setSelectedLayer] = useState<any>(null);
  const [drawnWkt, setDrawnWkt] = useState<string>('');

  return (
    <div className="flex flex-col h-screen w-screen bg-slate-900 text-slate-100 font-sans overflow-hidden">
      {/* Header Bar */}
      <header className="flex items-center justify-between px-6 py-3 bg-slate-950 border-b border-slate-800 shadow-md z-10">
        <div className="flex items-center space-x-3">
          <div className="bg-emerald-600 p-2 rounded-lg shadow">
            <MapIcon className="w-6 h-6 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold tracking-wide text-emerald-400">GIS Data Platform</h1>
            <p className="text-xs text-slate-400">Self-Service Spatial Query & Multi-DB Data Extraction System</p>
          </div>
        </div>

        {/* Navigation Tabs */}
        <div className="flex bg-slate-900 p-1 rounded-lg border border-slate-800">
          <button
            onClick={() => setActiveTab('map')}
            className={`flex items-center space-x-2 px-4 py-1.5 rounded-md text-sm font-medium transition ${
              activeTab === 'map' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <MapIcon className="w-4 h-4" />
            <span>Interactive Map</span>
          </button>
          <button
            onClick={() => setActiveTab('query')}
            className={`flex items-center space-x-2 px-4 py-1.5 rounded-md text-sm font-medium transition ${
              activeTab === 'query' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <Filter className="w-4 h-4" />
            <span>Query Builder</span>
          </button>
          <button
            onClick={() => setActiveTab('extraction')}
            className={`flex items-center space-x-2 px-4 py-1.5 rounded-md text-sm font-medium transition ${
              activeTab === 'extraction' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <Download className="w-4 h-4" />
            <span>Data Extraction</span>
          </button>
          <button
            onClick={() => setActiveTab('admin')}
            className={`flex items-center space-x-2 px-4 py-1.5 rounded-md text-sm font-medium transition ${
              activeTab === 'admin' ? 'bg-emerald-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <Shield className="w-4 h-4" />
            <span>Admin Portal</span>
          </button>
        </div>

        {/* User Info */}
        <div className="flex items-center space-x-3 text-xs border-l border-slate-800 pl-4">
          <div className="w-8 h-8 rounded-full bg-emerald-700 flex items-center justify-center font-bold text-white">
            GIS
          </div>
          <div>
            <div className="font-semibold text-slate-200">GIS Analyst</div>
            <div className="text-emerald-400">Oracle & PostGIS Access</div>
          </div>
        </div>
      </header>

      {/* Main Workspace Area */}
      <main className="flex-1 flex overflow-hidden">
        {activeTab === 'map' && (
          <div className="flex-1 flex w-full h-full relative">
            <LayerPanel onSelectLayer={setSelectedLayer} />
            <div className="flex-1 h-full relative">
              <MapComponent onDrawComplete={setDrawnWkt} />
            </div>
            {drawnWkt && (
              <div className="absolute bottom-4 right-4 bg-slate-900 border border-emerald-500/50 p-4 rounded-xl shadow-2xl z-20 max-w-md">
                <div className="flex justify-between items-center mb-2">
                  <span className="text-xs font-semibold text-emerald-400 uppercase tracking-wider">Redline Geometry Drawn</span>
                  <button onClick={() => setActiveTab('query')} className="text-xs bg-emerald-600 px-2 py-1 rounded text-white hover:bg-emerald-500">
                    Use in Query
                  </button>
                </div>
                <div className="text-xs text-slate-300 font-mono bg-slate-950 p-2 rounded max-h-20 overflow-y-auto break-all">
                  {drawnWkt}
                </div>
              </div>
            )}
          </div>
        )}

        {activeTab === 'query' && (
          <div className="flex-1 p-6 overflow-y-auto bg-slate-950">
            <QueryBuilderPanel drawnWkt={drawnWkt} selectedLayer={selectedLayer} />
          </div>
        )}

        {activeTab === 'extraction' && (
          <div className="flex-1 p-6 overflow-y-auto bg-slate-950">
            <ExtractionPanel />
          </div>
        )}

        {activeTab === 'admin' && (
          <div className="flex-1 p-6 overflow-y-auto bg-slate-950">
            <AdminPortal />
          </div>
        )}
      </main>
    </div>
  );
}
