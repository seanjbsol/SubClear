import React, { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View
} from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useAuth } from '../AuthContext';
import { colours } from '../theme';
import type { AuthStackParamList } from '../navigationTypes';

type Props = NativeStackScreenProps<AuthStackParamList, 'Login'>;

export function LoginScreen({ navigation }: Props) {
  const { login } = useAuth();
  const [email, setEmail] = useState('owner@demo.subclear.uk');
  const [password, setPassword] = useState('DemoPassword123!');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const onSubmit = async () => {
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sign-in failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
        <Text style={styles.kicker}>Main contractor supply-chain chase</Text>
        <Text style={styles.title}>SubClear</Text>
        <Text style={styles.lede}>
          Live register of subcontractor EL/PL/PI, SSIP and RAMS — traffic lights, not another SSIP scheme.
        </Text>

        <Text style={styles.label}>Email</Text>
        <TextInput
          autoCapitalize="none"
          autoCorrect={false}
          keyboardType="email-address"
          style={styles.input}
          value={email}
          onChangeText={setEmail}
        />
        <Text style={styles.label}>Password</Text>
        <TextInput
          secureTextEntry
          style={styles.input}
          value={password}
          onChangeText={setPassword}
        />

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <Pressable style={[styles.button, busy && styles.disabled]} onPress={onSubmit} disabled={busy}>
          <Text style={styles.buttonText}>{busy ? 'Signing in…' : 'Sign in'}</Text>
        </Pressable>

        <Pressable onPress={() => navigation.navigate('Register')}>
          <Text style={styles.link}>Register a main-contractor organisation</Text>
        </Pressable>

        <View style={styles.demo}>
          <Text style={styles.demoTitle}>Demo tenant (Humber Civils Ltd)</Text>
          <Text style={styles.demoText}>owner@demo.subclear.uk / DemoPassword123!</Text>
          <Text style={styles.demoText}>Second tenant: owner@northern.demo.subclear.uk</Text>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, backgroundColor: colours.paper },
  container: { padding: 24, paddingTop: 64 },
  kicker: { color: colours.amber, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 1, fontSize: 12 },
  title: { color: colours.navy, fontSize: 40, fontWeight: '800', marginTop: 8 },
  lede: { color: colours.muted, fontSize: 16, lineHeight: 22, marginTop: 8, marginBottom: 28 },
  label: { color: colours.muted, fontWeight: '600', marginBottom: 6 },
  input: {
    backgroundColor: colours.white,
    borderColor: colours.line,
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 12,
    fontSize: 16,
    marginBottom: 14,
    color: colours.ink
  },
  button: { backgroundColor: colours.navy, borderRadius: 10, paddingVertical: 14, alignItems: 'center', marginTop: 8 },
  disabled: { opacity: 0.6 },
  buttonText: { color: colours.white, fontWeight: '700', fontSize: 16 },
  link: { color: colours.navyMid, textAlign: 'center', marginTop: 18, fontWeight: '600' },
  error: { color: colours.red, marginBottom: 8 },
  demo: { marginTop: 32, padding: 16, backgroundColor: colours.white, borderRadius: 12, borderColor: colours.line, borderWidth: 1 },
  demoTitle: { fontWeight: '700', color: colours.navy, marginBottom: 6 },
  demoText: { color: colours.muted, fontSize: 13 }
});
