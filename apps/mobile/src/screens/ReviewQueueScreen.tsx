import React, { useCallback, useState } from 'react';
import { Alert, FlatList, Pressable, RefreshControl, StyleSheet, Text, TextInput, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours, formatUkDate } from '../theme';
import type { ReviewQueueItem } from '../types';
import type { ReviewStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<ReviewStackParamList, 'ReviewQueue'>;

export function ReviewQueueScreen({ navigation }: Props) {
  const { request, canReview, entitlements } = useAuth();
  const [rows, setRows] = useState<ReviewQueueItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [comment, setComment] = useState('');
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      setRows(await request<ReviewQueueItem[]>('/api/review-queue'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load the review queue.');
      setRows([]);
    }
  }, [request]);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load])
  );

  const decide = async (item: ReviewQueueItem, decision: 'approved' | 'rejected') => {
    if (!canReview) {
      return;
    }
    try {
      await request(`/api/documents/${item.documentId}/review`, {
        method: 'POST',
        body: { decision, comment: comment.trim() || null }
      });
      setComment('');
      await load();
    } catch (err) {
      Alert.alert('Could not save review', err instanceof Error ? err.message : 'Try again.');
    }
  };

  return (
    <View style={styles.flex}>
      {entitlements && !entitlements.hasReviewQueue ? (
        <Text style={styles.upgrade}>
          Expert review is a SubClear Pro feature. Upgrade under Settings to approve or reject packs.
        </Text>
      ) : null}
      {canReview ? (
        <View style={styles.composer}>
          <Text style={styles.label}>Review comment (optional)</Text>
          <TextInput
            style={styles.input}
            value={comment}
            onChangeText={setComment}
            placeholder="e.g. Please send the full EL schedule, not the summary page."
            multiline
          />
        </View>
      ) : (
        <Text style={styles.meta}>Only Owners, Admins and Reviewers can approve or reject documents.</Text>
      )}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={rows ?? []}
        keyExtractor={(item) => item.documentId}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={async () => {
              setRefreshing(true);
              await load();
              setRefreshing(false);
            }}
          />
        }
        renderItem={({ item }) => (
          <View style={styles.card}>
            <Pressable onPress={() => navigation.navigate('SubcontractorDetail', { id: item.subcontractorId, name: item.subcontractorName })}>
              <Text style={styles.name}>{item.subcontractorName}</Text>
              <Text style={styles.rowTitle}>{item.title}</Text>
              <Text style={styles.meta}>
                {item.typeLabel} · Expiry {formatUkDate(item.expiryDate)}
                {item.fileName ? ` · ${item.fileName}` : ''}
              </Text>
            </Pressable>
            {canReview && entitlements?.hasReviewQueue ? (
              <View style={styles.actions}>
                <Pressable style={styles.approve} onPress={() => void decide(item, 'approved')}>
                  <Text style={styles.approveText}>Approve</Text>
                </Pressable>
                <Pressable style={styles.reject} onPress={() => void decide(item, 'rejected')}>
                  <Text style={styles.rejectText}>Reject</Text>
                </Pressable>
              </View>
            ) : null}
          </View>
        )}
        ListEmptyComponent={
          <Text style={styles.empty}>{rows === null ? 'Loading review queue…' : 'No documents waiting for review.'}</Text>
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  composer: { margin: 16, marginBottom: 0, backgroundColor: colours.white, borderRadius: 12, padding: 12, borderWidth: 1, borderColor: colours.line },
  label: { color: colours.muted, fontWeight: '600', marginBottom: 6 },
  input: { minHeight: 60, color: colours.ink },
  list: { padding: 16, paddingBottom: 40 },
  card: { backgroundColor: colours.white, borderRadius: 12, padding: 14, marginBottom: 10, borderWidth: 1, borderColor: colours.line },
  name: { fontWeight: '800', color: colours.navy, fontSize: 16 },
  rowTitle: { fontWeight: '700', color: colours.navy, marginTop: 4 },
  meta: { color: colours.muted, marginTop: 6, marginHorizontal: 16 },
  actions: { flexDirection: 'row', gap: 8, marginTop: 12 },
  approve: { backgroundColor: colours.green, borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8 },
  approveText: { color: colours.white, fontWeight: '700' },
  reject: { backgroundColor: colours.redSoft, borderRadius: 8, paddingHorizontal: 12, paddingVertical: 8 },
  rejectText: { color: colours.red, fontWeight: '700' },
  empty: { color: colours.muted, textAlign: 'center', marginTop: 40 },
  error: { color: colours.red, marginHorizontal: 16, marginTop: 8 },
  upgrade: { margin: 16, color: colours.amber, fontWeight: '600' }
});
