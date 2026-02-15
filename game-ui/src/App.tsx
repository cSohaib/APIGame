import './App.css';
import { GameView } from './components/GameView';
import { useGameSocket } from './features/game/useGameSocket';

function App() {
  useGameSocket();
  return <GameView />;
}

export default App;
