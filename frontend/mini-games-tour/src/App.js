import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Home from './pages/home/Home';
import GameEntry from './components/GameEntry';
import WaitingRoom from './pages/WaitingRoom/WaitingRoom';
import SessionRoom from './pages/SessionRoom/SessionRoom';
import ReturnToGameBanner from './utils/ReturnToGameBanner';

function App() {
  return (
    <Router>
      <ReturnToGameBanner />
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/rock-paper-scissors" element={<GameEntry />} />
        <Route path="/four-in-a-row" element={<GameEntry />} />
        <Route path="/pair-matching" element={<GameEntry />} />
        <Route path="/:gameType/waiting/:code" element={<WaitingRoom />} />
        <Route path="/:gameType/session/:code" element={<SessionRoom />} />
      </Routes>
    </Router>
  );
}

export default App;
