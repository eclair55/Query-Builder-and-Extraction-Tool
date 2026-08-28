import React, { useEffect, useRef, useState } from 'react';
import 'ol/ol.css';
import Map from 'ol/Map';
import View from 'ol/View';
import TileLayer from 'ol/layer/Tile';
import OSM from 'ol/source/OSM';
import TileWMS from 'ol/source/TileWMS';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import Draw from 'ol/interaction/Draw';
import { WKT } from 'ol/format';
import { MousePosition } from 'ol/control';
import { createStringXY } from 'ol/coordinate';
import { Pencil, Square, Circle, MapPin, Trash2, Ruler, Layers } from 'lucide-react';

interface MapProps {
  onDrawComplete: (wkt: string) => void;
}

export default function MapComponent({ onDrawComplete }: MapProps) {
  const mapRef = useRef<HTMLDivElement>(null);
  const mapInstance = useRef<Map | null>(null);
  const vectorSource = useRef(new VectorSource());
  const drawInteraction = useRef<Draw | null>(null);
  const [activeTool, setActiveTool] = useState<string>('none');

  useEffect(() => {
    if (!mapRef.current) return;

    const vectorLayer = new VectorLayer({
      source: vectorSource.current,
    });

    const map = new Map({
      target: mapRef.current,
      layers: [
        new TileLayer({
          source: new OSM(),
        }),
        vectorLayer,
      ],
      view: new View({
        center: [13472481, 1637785], // Philippines / Manila EPSG:3857 coords
        zoom: 12,
      }),
    });

    const mousePositionControl = new MousePosition({
      coordinateFormat: createStringXY(4),
      projection: 'EPSG:4326',
      className: 'custom-mouse-position',
      target: document.getElementById('mouse-position') as HTMLElement,
    });
    map.addControl(mousePositionControl);

    mapInstance.current = map;

    return () => map.setTarget(undefined);
  }, []);

  const startDraw = (type: string) => {
    if (!mapInstance.current) return;
    if (drawInteraction.current) {
      mapInstance.current.removeInteraction(drawInteraction.current);
    }

    setActiveTool(type);
    if (type === 'none') return;

    let drawType: any = 'Point';
    if (type === 'line') drawType = 'LineString';
    if (type === 'polygon') drawType = 'Polygon';
    if (type === 'box') drawType = 'Circle'; // geometryFunction for Box in OL

    const draw = new Draw({
      source: vectorSource.current,
      type: drawType,
    });

    draw.on('drawend', (event) => {
      const feature = event.feature;
      const format = new WKT();
      const wkt = format.writeGeometry(feature.getGeometry()!.transform('EPSG:3857', 'EPSG:4326'));
      onDrawComplete(wkt);
    });

    mapInstance.current.addInteraction(draw);
    drawInteraction.current = draw;
  };

  const clearDrawing = () => {
    vectorSource.current.clear();
    onDrawComplete('');
    startDraw('none');
  };

  return (
    <div className="w-full h-full relative">
      <div ref={mapRef} className="w-full h-full bg-slate-950" />

      {/* Floating Map Tools Bar */}
      <div className="absolute top-4 left-4 bg-slate-900/90 backdrop-blur border border-slate-700/80 p-2 rounded-xl shadow-2xl flex items-center space-x-1 z-10">
        <button
          onClick={() => startDraw('point')}
          className={`p-2 rounded-lg text-xs font-medium flex items-center space-x-1 transition ${
            activeTool === 'point' ? 'bg-emerald-600 text-white' : 'text-slate-300 hover:bg-slate-800'
          }`}
          title="Draw Point"
        >
          <MapPin className="w-4 h-4" />
          <span>Point</span>
        </button>
        <button
          onClick={() => startDraw('line')}
          className={`p-2 rounded-lg text-xs font-medium flex items-center space-x-1 transition ${
            activeTool === 'line' ? 'bg-emerald-600 text-white' : 'text-slate-300 hover:bg-slate-800'
          }`}
          title="Draw Line"
        >
          <Pencil className="w-4 h-4" />
          <span>Line</span>
        </button>
        <button
          onClick={() => startDraw('polygon')}
          className={`p-2 rounded-lg text-xs font-medium flex items-center space-x-1 transition ${
            activeTool === 'polygon' ? 'bg-emerald-600 text-white' : 'text-slate-300 hover:bg-slate-800'
          }`}
          title="Draw Polygon"
        >
          <Square className="w-4 h-4" />
          <span>Polygon</span>
        </button>
        <div className="h-5 w-px bg-slate-700 mx-1" />
        <button
          onClick={clearDrawing}
          className="p-2 rounded-lg text-xs font-medium text-rose-400 hover:bg-rose-950/50 flex items-center space-x-1"
          title="Clear Drawn Geometry"
        >
          <Trash2 className="w-4 h-4" />
          <span>Clear</span>
        </button>
      </div>

      {/* Coordinates Display */}
      <div className="absolute bottom-4 left-4 bg-slate-900/80 backdrop-blur px-3 py-1.5 rounded-md text-xs text-slate-300 border border-slate-800 flex items-center space-x-2 z-10">
        <span className="font-semibold text-emerald-400">EPSG:4326</span>
        <div id="mouse-position" className="font-mono text-slate-300" />
      </div>
    </div>
  );
}
