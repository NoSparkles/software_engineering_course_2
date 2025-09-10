export default function getShuffledCards() {
  const baseCards = Array.from({ length: 9 }, (_, i) => ({
    id: i,
    image: `🃏${i + 1}`
  }));

  const pairedCards = [...baseCards, ...baseCards].map((card, index) => ({
    ...card,
    key: index,
    matched: false
  }));

  const shuffled = pairedCards.sort(() => Math.random() - 0.5);

  return shuffled;
}