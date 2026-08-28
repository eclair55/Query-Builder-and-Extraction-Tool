import React, { useState } from 'react';
import { Layers, Eye, EyeOff, Search, Info, ShieldCheck } from 'lucide-react';

interface LayerPanelProps {
  onSelectLayer: (layer: any) => void;
}

export default function LayerPanel({ onSelectLayer }: LayerPanelProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const [layers, setLayers] = useState([
    {
      id: '1',
      title: 'ODN Facility Points (OLT/NAP)',
      workspace: 'PPGIS',
      table: 'ODN_CONT_GEOM',
      visible: true,
      geomType: 'Point',
      permissions: { view: true, query: true, extract: true, spatial: true, download: true }
    },
    {
      id: '2',
      title: 'Fiber Cable Sheaths',
      workspace: 'PPGIS',
      table: 'FIB_CABLE_SHEATH_GEOM',
      visible: true,
      geomType: 'LineString',
      permissions: { view: true, query: true, extract: true, spatial: true, download: true }
    },
    {
      id: '3',
      title: 'Cadastral Parcels',
      workspace: 'PPGIS',
      table: 'LOTS_GEOM',
      visible: false,
      geomType: 'Polygon',
      permissions: { view: true, query: true, extract: true, spatial: false, download: false }
    }
  ]);

  const toggleVisibility = (id: string) => {
    setLayers(layers.map(l => l.id === id ? { ...l, visible: !l.visible } : l));
  };

  return (
    <div className="w-80 bg-slate-900 border-r border-slate-800 flex flex-col h-full z-10 shadow-xl">
      <div className="p-4 border-b border-slate-800">
        <div className="flex items-center space-x-2 text-emerald-400 font-bold mb-3">
          <Layers className="w-5 h-5" />
          <span>GIS Layers Catalog</span>
        </div>
        <div className="relative">
          <Search className="w-4 h-4 absolute left-3 top-2.5 text-slate-400" />
          <input
            type="text"
            placeholder="Search GeoServer layers..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full bg-slate-950 border border-slate-800 rounded-lg pl-9 pr-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-emerald-500"
          />
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-3 space-y-2">
        {layers
          .filter(l => l.title.toLowerCase().includes(searchTerm.toLowerCase()))
          .map(layer => (
            <div
              key={layer.id}
              onClick={() => onSelectLayer(layer)}
              className="bg-slate-950 border border-slate-800 hover:border-emerald-500/50 p-3 rounded-xl transition cursor-pointer group"
            >
              <div className="flex items-start justify-between">
                <div className="flex items-center space-x-2">
                  <button
                    onClick={(e) => { e.stopPropagation(); toggleVisibility(layer.id); }}
                    className="text-slate-400 hover:text-emerald-400"
                  >
                    {layer.visible ? <Eye className="w-4 h-4 text-emerald-400" /> : <EyeOff className="w-4 h-4 text-slate-500" />}
                  </button>
                  <div>
                    <h4 className="text-xs font-semibold text-slate-200 group-hover:text-emerald-300">{layer.title}</h4>
                    <span className="text-[10px] text-slate-500 font-mono">{layer.workspace}:{layer.table}</span>
                  </div>
                </div>
                <span className="text-[10px] font-medium bg-slate-800 text-emerald-400 px-2 py-0.5 rounded-full border border-slate-700">
                  {layer.geomType}
                </span>
              </div>

              {/* Granular Permission Badges */}
              <div className="mt-3 pt-2 border-t border-slate-800/80 flex items-center justify-between text-[10px] text-slate-400">
                <div className="flex items-center space-x-1 text-slate-400">
                  <ShieldCheck className="w-3 h-3 text-emerald-400" />
                  <span>Role Permissions:</span>
                </div>
                <div className="flex space-x-1 font-mono">
                  <span className={layer.permissions.view ? 'text-emerald-400' : 'text-slate-600'}>V</span>
                  <span className={layer.permissions.query ? 'text-emerald-400' : 'text-slate-600'}>Q</span>
                  <span className={layer.permissions.extract ? 'text-emerald-400' : 'text-slate-600'}>E</span>
                  <span className={layer.permissions.spatial ? 'text-emerald-400' : 'text-slate-600'}>S</span>
                  <span className={layer.permissions.download ? 'text-emerald-400' : 'text-slate-600'}>D</span>
                </div>
              </div>
            </div>
          ))}
      </div>
    </div>
  );
}
